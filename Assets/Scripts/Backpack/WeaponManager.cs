using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>武器系统唯一数据源。槽位 + 事件通知</summary>
public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    /// <summary>0-2武器槽</summary>
    public ItemSO[] slots = new ItemSO[3];
    public int currentIndex = -1;

    public event Action OnChanged;

    private Dictionary<ItemSO, GameObject> cachedModels = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (slots == null || slots.Length != 3)
            slots = new ItemSO[3];
    }

    /// <summary>获取当前槽位的ItemSO</summary>
    public ItemSO GetCurrent() => currentIndex >= 0 && currentIndex < 3 ? slots[currentIndex] : null;

    /// <summary>装备指定槽位</summary>
    public void EquipSlot(int index)
    {
        if (index < 0 || index >= 3) return;
        if (index == currentIndex) return;

        ItemSO item = slots[index];
        if (item == null) return;

        currentIndex = index;
        OnChanged?.Invoke();
    }

    /// <summary>从背包刷新武器槽（遍历itemList，武器填0-2）</summary>
    public void RefreshSlots()
    {
        var all = GameInventory.InventoryService.Instance.GetAllItemSOs();
        int wIdx = 0;
        var seen = new HashSet<ItemSO>();

        for (int i = 0; i < 3; i++) slots[i] = null;

        foreach (var item in all)
        {
            if (seen.Contains(item)) continue;
            seen.Add(item);

            if (item.itemType == ItemType.Weapon && wIdx < 3)
                slots[wIdx++] = item;
        }

        if (currentIndex < 0 || currentIndex >= 3 || slots[currentIndex] == null)
            currentIndex = -1;

        Debug.Log("RefreshSlots: [0]=" + (slots[0]?.name ?? "null") + " [1]=" + (slots[1]?.name ?? "null") + " [2]=" + (slots[2]?.name ?? "null"));
        OnChanged?.Invoke();
    }

    /// <summary>缓存/获取模型实例。forceRebuild=true 时会销毁旧的重新创建</summary>
    public GameObject GetModel(ItemSO item, bool forceRebuild = false)
    {
        if (forceRebuild && cachedModels.ContainsKey(item))
        {
            Destroy(cachedModels[item]);
            cachedModels.Remove(item);
        }
        if (!cachedModels.ContainsKey(item))
        {
            var go = Instantiate(item.prefab);
            go.SetActive(false);
            cachedModels[item] = go;
        }
        return cachedModels[item];
    }

    /// <summary>按顺序装填到第一个空槽 (0→1→2)，返回槽位号，-1表示已满</summary>
    public int EquipToNextSlot(ItemSO item)
    {
        for (int i = 0; i < 3; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                EquipSlot(i);
                return i;
            }
        }
        return -1;
    }

    /// <summary>清空指定槽位</summary>
    public void ClearSlot(int index)
    {
        if (index < 0 || index >= 3) return;
        slots[index] = null;
        if (currentIndex == index)
            currentIndex = -1;
        OnChanged?.Invoke();
    }
}
