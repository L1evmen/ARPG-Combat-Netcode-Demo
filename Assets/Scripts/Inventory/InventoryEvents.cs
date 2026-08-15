using System;
using UnityEngine;

namespace GameInventory
{
    /// <summary>
    /// 背包系统事件总线 —— 所有物品增删改、装备切换、快捷栏变更均通过此静态事件通知 UI。
    ///
    /// 设计原则：
    /// 1. 数据层不引用 UI 层 —— 事件在此解耦。
    /// 2. 零 GC（Action vs UnityEvent）。
    /// 3. 新 UI 面板只需订阅事件，不改数据代码（OCP）。
    /// </summary>
    public static class InventoryEvents
    {
        // ---- 物品增删 ----
        public static event Action<ItemSO, int> OnItemAdded;       // (物品, 当前数量)
        public static event Action<ItemSO, int> OnItemRemoved;     // (物品, 剩余数量)
        public static event Action<ItemSO, int> OnItemCountChanged; // (物品, 新数量)

        // ---- 装备 ----
        public static event Action<EquipmentSlotType, ItemSO> OnEquipmentChanged;  // (槽位, 装备)
        public static event Action<EquipmentSlotType, ItemSO> OnEquipmentRemoved;  // (槽位, 卸下的装备)
        public static event Action OnSetBonusChanged;

        // ---- 快捷栏 ----
        public static event Action<int, ItemSO> OnQuickSlotChanged;  // (槽位索引0-3, 物品)
        public static event Action<int, int> OnQuickSlotCountChanged; // (槽位索引, 剩余数量)

        // ---- 精魄/法宝 ----
        public static event Action<ItemSO> OnSpiritEquipped;
        public static event Action<ItemSO> OnTreasureEquipped;

        // ---- 新物品提示 ----
        public static event Action<ItemSO> OnNewItemObtained;

        // ---- 容量 ----
        public static event Action<int, int> OnCapacityChanged;  // (当前量, 最大容量)

        // ---- Fire 方法 ----

        public static void FireItemAdded(ItemSO item, int count)
            => OnItemAdded?.Invoke(item, count);

        public static void FireItemRemoved(ItemSO item, int remaining)
            => OnItemRemoved?.Invoke(item, remaining);

        public static void FireItemCountChanged(ItemSO item, int newCount)
            => OnItemCountChanged?.Invoke(item, newCount);

        public static void FireEquipmentChanged(EquipmentSlotType slot, ItemSO item)
            => OnEquipmentChanged?.Invoke(slot, item);

        public static void FireEquipmentRemoved(EquipmentSlotType slot, ItemSO item)
            => OnEquipmentRemoved?.Invoke(slot, item);

        public static void FireSetBonusChanged()
            => OnSetBonusChanged?.Invoke();

        public static void FireQuickSlotChanged(int index, ItemSO item)
            => OnQuickSlotChanged?.Invoke(index, item);

        public static void FireQuickSlotCountChanged(int index, int count)
            => OnQuickSlotCountChanged?.Invoke(index, count);

        public static void FireSpiritEquipped(ItemSO item)
            => OnSpiritEquipped?.Invoke(item);

        public static void FireTreasureEquipped(ItemSO item)
            => OnTreasureEquipped?.Invoke(item);

        public static void FireNewItemObtained(ItemSO item)
            => OnNewItemObtained?.Invoke(item);

        public static void FireCapacityChanged(int current, int max)
            => OnCapacityChanged?.Invoke(current, max);
    }
}
