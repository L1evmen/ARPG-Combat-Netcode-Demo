#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using ARPG.StateSync;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ARPG.Networking
{
    public sealed class NetworkSyncSmokeTest : MonoBehaviour
    {
        private const float Timeout = 12f;
        private const float MoveDistance = 1.5f;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int InputXHash = Animator.StringToHash("inputX");
        private static readonly int AttackingHash = Animator.StringToHash("IsAttacking");
        private static bool _failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sync-smoke") < 0)
                return;

            DontDestroyOnLoad(new GameObject(nameof(NetworkSyncSmokeTest))
                .AddComponent<NetworkSyncSmokeTest>());
        }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while ((NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            {
                Fail("connection timeout");
                yield break;
            }

            if (NetworkManager.Singleton.IsHost)
            {
                yield return VerifyRemote("HOST_RECEIVE_CLIENT");
                if (_failed) yield break;
                yield return new WaitForSecondsRealtime(0.5f);
                yield return DriveLocal("HOST");
                yield return DriveBoss();
            }
            else
            {
                yield return DriveLocal("CLIENT");
                yield return VerifyRemote("CLIENT_RECEIVE_HOST");
                if (_failed) yield break;
                yield return VerifyBoss();
                if (_failed) yield break;
            }

            Debug.Log($"[SyncSmoke] {(NetworkManager.Singleton.IsHost ? "HOST" : "CLIENT")}_PASS");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(0);
        }

        private static IEnumerator DriveLocal(string role)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Animator animator = player.GetComponent<Animator>();
            player.GetComponent<PlayerController>().enabled = false;
            player.GetComponent<InputController>().enabled = false;
            player.GetComponent<CombatV2.ComboManager>().enabled = false;

            yield return new WaitForSecondsRealtime(2f);

            Vector3 origin = player.transform.position;
            Vector3 target = origin + Vector3.right * MoveDistance;
            Quaternion originRotation = player.transform.rotation;
            Quaternion targetRotation = originRotation * Quaternion.Euler(0f, 90f, 0f);
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < 1f)
            {
                float t = Time.realtimeSinceStartup - startedAt;
                player.transform.position = Vector3.Lerp(origin, target, t);
                player.transform.rotation = Quaternion.Slerp(originRotation, targetRotation, t);
                animator.SetFloat(SpeedHash, 4f);
                animator.SetFloat(InputXHash, 1f);
                yield return null;
            }

            player.transform.SetPositionAndRotation(target, targetRotation);
            Debug.Log($"[SyncSmoke] {role}_MOVE origin={origin} target={target}");

            animator.SetBool(AttackingHash, true);
            Debug.Log($"[SyncSmoke] {role}_ATTACK true");
            yield return new WaitForSecondsRealtime(1f);
            animator.SetBool(AttackingHash, false);
        }

        private static IEnumerator VerifyRemote(string phase)
        {
            NetworkPlayerProxy remote = null;
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (remote == null && Time.realtimeSinceStartup < deadline)
            {
                NetworkPlayerProxy[] players = FindObjectsByType<NetworkPlayerProxy>(FindObjectsSortMode.None);
                for (int i = 0; i < players.Length; i++)
                {
                    if (!players[i].IsOwner)
                    {
                        remote = players[i];
                        break;
                    }
                }
                yield return null;
            }

            if (remote == null)
            {
                Fail("remote player timeout");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.5f);

            GameObject replica = GameObject.Find($"Remote Player {remote.OwnerClientId}");
            if (replica == null)
            {
                Fail("remote replica missing");
                yield break;
            }

            CharacterStateSynchronizer synchronizer = replica.GetComponent<CharacterStateSynchronizer>();
            Vector3 sourceOrigin = remote.transform.position;
            Vector3 replicaOrigin = replica.transform.position;
            Quaternion sourceOriginRotation = remote.transform.rotation;
            Quaternion replicaOriginRotation = replica.transform.rotation;
            float maxSourceMovement = 0f;
            float maxReplicaMovement = 0f;
            float maxSourceRotation = 0f;
            float maxReplicaRotation = 0f;
            float replicaError = float.MaxValue;
            bool attackReceived = false;
            bool locomotionReceived = false;

            while (Time.realtimeSinceStartup < deadline)
            {
                maxSourceMovement = Mathf.Max(
                    maxSourceMovement, Vector3.Distance(sourceOrigin, remote.transform.position));
                maxReplicaMovement = Mathf.Max(
                    maxReplicaMovement, Vector3.Distance(replicaOrigin, replica.transform.position));
                maxSourceRotation = Mathf.Max(
                    maxSourceRotation, Quaternion.Angle(sourceOriginRotation, remote.transform.rotation));
                maxReplicaRotation = Mathf.Max(
                    maxReplicaRotation, Quaternion.Angle(replicaOriginRotation, replica.transform.rotation));
                replicaError = NetworkManager.Singleton.IsServer
                    ? Vector3.Distance(replica.transform.position, remote.transform.position)
                    : synchronizer.LastPositionError;
                Animator animator = replica.GetComponent<Animator>();
                attackReceived |= animator.GetBool(AttackingHash);
                locomotionReceived |= animator.GetFloat(SpeedHash) >= 3f && animator.GetFloat(InputXHash) >= 0.8f;

                bool sourceAccepted = !NetworkManager.Singleton.IsServer || maxSourceMovement >= 1f;
                bool sourceRotated = !NetworkManager.Singleton.IsServer || maxSourceRotation >= 45f;
                if (sourceAccepted && sourceRotated && maxReplicaMovement >= 1f && maxReplicaRotation >= 45f &&
                    replicaError <= 0.35f && locomotionReceived && attackReceived)
                {
                    Debug.Log($"[SyncSmoke] {phase}_PASS owner={remote.OwnerClientId} " +
                              $"sourceMove={maxSourceMovement:F2}m replicaMove={maxReplicaMovement:F2}m " +
                              $"sourceRot={maxSourceRotation:F1}deg replicaRot={maxReplicaRotation:F1}deg " +
                              $"replicaError={replicaError:F3}m locomotion=true attack=true");
                    yield break;
                }

                yield return null;
            }

            Fail($"sourceMove={maxSourceMovement:F2}m replicaMove={maxReplicaMovement:F2}m " +
                 $"sourceRot={maxSourceRotation:F1}deg replicaRot={maxReplicaRotation:F1}deg " +
                 $"replicaError={replicaError:F3}m locomotion={locomotionReceived} attack={attackReceived}");
        }

        private static IEnumerator DriveBoss()
        {
            NetworkBossSynchronizer boss = NetworkBossSynchronizer.Instance;
            BossController controller = boss.GetComponent<BossController>();
            NavMeshAgent agent = boss.GetComponent<NavMeshAgent>();
            controller.enabled = false;
            agent.enabled = false;

            yield return new WaitForSecondsRealtime(1f);

            Vector3 origin = boss.transform.position;
            Vector3 target = origin + Vector3.forward * MoveDistance;
            Quaternion originRotation = boss.transform.rotation;
            Quaternion targetRotation = originRotation * Quaternion.Euler(0f, 90f, 0f);
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < 1f)
            {
                float t = Time.realtimeSinceStartup - startedAt;
                boss.transform.position = Vector3.Lerp(origin, target, t);
                boss.transform.rotation = Quaternion.Slerp(originRotation, targetRotation, t);
                yield return null;
            }

            boss.transform.SetPositionAndRotation(target, targetRotation);
            Debug.Log($"[SyncSmoke] HOST_BOSS_MOVE origin={origin} target={target}");
            yield return new WaitForSecondsRealtime(2f);
        }

        private static IEnumerator VerifyBoss()
        {
            NetworkBossSynchronizer boss = NetworkBossSynchronizer.Instance;
            CharacterStateSynchronizer synchronizer = boss.GetComponent<CharacterStateSynchronizer>();
            yield return new WaitForSecondsRealtime(0.5f);

            Vector3 origin = boss.transform.position;
            Quaternion originRotation = boss.transform.rotation;
            float maxMovement = 0f;
            float maxRotation = 0f;
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                maxMovement = Mathf.Max(maxMovement, Vector3.Distance(origin, boss.transform.position));
                maxRotation = Mathf.Max(maxRotation, Quaternion.Angle(originRotation, boss.transform.rotation));
                if (maxMovement >= 1f && maxRotation >= 45f && synchronizer.LastPositionError <= 0.35f)
                {
                    Debug.Log($"[SyncSmoke] CLIENT_RECEIVE_BOSS_PASS movement={maxMovement:F2}m " +
                              $"rotation={maxRotation:F1}deg error={synchronizer.LastPositionError:F3}m");
                    yield break;
                }

                yield return null;
            }

            Fail($"bossMove={maxMovement:F2}m bossRot={maxRotation:F1}deg " +
                 $"error={synchronizer.LastPositionError:F3}m");
        }

        private static void Fail(string reason)
        {
            _failed = true;
            Debug.LogError($"[SyncSmoke] FAIL {reason}");
            Application.Quit(2);
        }
    }
}
#endif
