using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnTime = 5f;
    public float spawnRadius = 10f;
    [Tooltip("地面层级，射线只检测这一层")]
    public LayerMask groundLayer = ~0; // 默认检测所有层，Inspector 中改为 Ground 层
    private float spawnTimer;

    void Start()
    {
        SpawnEnemy();
    }

    void Update()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer > spawnTime)
        {
            spawnTimer = 0;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        Vector3 origin = transform.position;
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0;
        Vector3 candidate = origin + randomOffset;

        // 射线检测地面（只检测指定层级，避免打到空中碰撞体）
        bool hitGround = Physics.Raycast(candidate + Vector3.up * 50f, Vector3.down, out RaycastHit rayHit, 100f, groundLayer);
        if (hitGround)
        {
            candidate.y = rayHit.point.y;
        }

        // NavMesh 采样微调水平位置
        if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            candidate = navHit.position;
        }

        Debug.Log($"[EnemySpawner] SpawnEnemy: spawner={origin}, candidate={candidate}, hitGround={hitGround}, rayHit={rayHit.point}");
        GameObject enemy = Instantiate(enemyPrefab, candidate, Quaternion.identity);

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.Warp(candidate);
    }
}
