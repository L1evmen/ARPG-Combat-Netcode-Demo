using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDBManager : MonoBehaviour
{
    public static ItemDBManager Instance { get; private set; }

    public ItemDBSO itemDB;

    // 预实例化缓存池
    private Dictionary<ItemSO, Queue<GameObject>> pool = new Dictionary<ItemSO, Queue<GameObject>>();
    private const int PoolSize = 5;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); return;
        }
        Instance = this;
        PreloadPool();
    }

    void PreloadPool()
    {
        if (itemDB == null || itemDB.itemList == null) return;
        foreach (var item in itemDB.itemList)
        {
            if (item == null || item.prefab == null) continue;
            var queue = new Queue<GameObject>();
            for (int i = 0; i < PoolSize; i++)
            {
                queue.Enqueue(CreatePoolItem(item));
            }
            pool[item] = queue;
        }
        Debug.Log("ItemDBManager: pooled " + pool.Count + " item types x" + PoolSize);
    }

    GameObject CreatePoolItem(ItemSO item)
    {
        var go = Instantiate(item.prefab);
        go.tag = Tag.INTERACTABLE;

        // 禁用武器组件，世界物品不需要武器逻辑
        // 否则 ScytheWeapon.Start() 会把碰撞体关掉导致物品穿模掉落
        Weapon w = go.GetComponent<Weapon>();
        if (w != null) w.enabled = false;

        // 提前挂载 PickableObject，避免首次 spawn 时 AddComponent 触发 Awake 链卡顿
        PickableObject po = go.GetComponent<PickableObject>();
        if (po == null) po = go.AddComponent<PickableObject>();
        po.itemSO = item;

        Animator anim = go.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        // 确保所有子碰撞体都正确启用（ScytheWeapon.Start 可能已关掉子物体的碰撞体）
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        Rigidbody rb = go.GetComponentInChildren<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        go.SetActive(false);
        DontDestroyOnLoad(go);
        return go;
    }

    public ItemSO GetRandomItem()
    {
        int randomIndex = Random.Range(0, itemDB.itemList.Count);
        return itemDB.itemList[randomIndex];
    }

    public GameObject TakeFromPool(ItemSO item)
    {
        GameObject go;
        if (pool.ContainsKey(item) && pool[item].Count > 0)
        {
            go = pool[item].Dequeue();
            StartCoroutine(RefillPool(item));
        }
        else
        {
            go = CreatePoolItem(item);
        }

        // 确保掉落物配置正确（兼容预加载的老对象）
        go.SetActive(true);
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = true;
            c.isTrigger = false;
        }
        Rigidbody rb = go.GetComponentInChildren<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        return go;
    }

    System.Collections.IEnumerator RefillPool(ItemSO item)
    {
        yield return null;
        if (!pool.ContainsKey(item)) pool[item] = new Queue<GameObject>();
        pool[item].Enqueue(CreatePoolItem(item));
    }

    // ==================== 掉落物管理 ====================
    private static readonly List<Collider> _dropColliders = new List<Collider>();

    /// <summary>
    /// 在指定位置掉落 count 个随机物品，抛物线飞出、Trigger 碰撞体不阻挡玩家。
    /// </summary>
    public void DropItems(Vector3 position, int count = 3)
    {
        StartCoroutine(DropItemsRoutine(position, count));
    }

    System.Collections.IEnumerator DropItemsRoutine(Vector3 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var item = GetRandomItem();
            if (item == null) continue;

            var go = TakeFromPool(item);
            if (go == null) continue;

            go.SetActive(true);
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);

            go.transform.position = position + Vector3.up * 0.5f;
            go.transform.rotation = Quaternion.identity;

            var po = go.GetComponent<PickableObject>();
            if (po != null) po.itemSO = item;

            // 非 Trigger 碰撞体：参与物理碰撞（落地），由 PlayerPick.OnCollisionEnter 触发拾取
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = true;
                c.isTrigger = false;
            }
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.3f, 0.3f, 0.3f);
                bc.isTrigger = true;
            }

            // 掉落物之间互不碰撞
            _dropColliders.RemoveAll(c => c == null);
            var newCols = go.GetComponentsInChildren<Collider>();
            foreach (var existing in _dropColliders)
            {
                if (existing == null) continue;
                foreach (var nc in newCols)
                    if (nc != null) Physics.IgnoreCollision(nc, existing, true);
            }
            _dropColliders.AddRange(newCols);

            // 抛物线：随机水平方向 + 向上
            var rb = go.GetComponentInChildren<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 dir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            rb.velocity = dir * 2f + Vector3.up * 5f;
            StartCoroutine(FreezeAfterDelay(rb, 1.5f));
            yield return null; // 每个物品间隔一帧，避免同帧卡顿
        }
        yield break;
    }

    System.Collections.IEnumerator FreezeAfterDelay(Rigidbody rb, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }
}
