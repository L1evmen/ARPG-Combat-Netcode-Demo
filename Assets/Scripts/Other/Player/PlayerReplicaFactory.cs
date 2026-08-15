using UnityEngine;

namespace ARPG.StateSync
{
    public static class PlayerReplicaFactory
    {
        public static GameObject Create(
            Transform source,
            Animator sourceAnimator,
            string name,
            Color? tint = null)
        {
            GameObject replica = new GameObject(name);
            replica.transform.SetPositionAndRotation(source.position, source.rotation);

            for (int i = 0; i < source.childCount; i++)
                Object.Instantiate(source.GetChild(i).gameObject, replica.transform, false);

            DisableGameplayComponents(replica);

            Animator animator = replica.AddComponent<Animator>();
            animator.avatar = sourceAnimator.avatar;
            animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            animator.applyRootMotion = false;
            animator.updateMode = sourceAnimator.updateMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);

            if (tint.HasValue)
                TintRenderers(replica, tint.Value);

            return replica;
        }

        public static void DisableGameplayComponents(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }

            AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
                audioSources[i].enabled = false;

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
                lights[i].enabled = false;
        }

        private static void TintRenderers(GameObject root, Color tint)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                renderers[i].SetPropertyBlock(block);
                block.Clear();
            }
        }
    }
}
