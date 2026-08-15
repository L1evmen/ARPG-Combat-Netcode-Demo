using System.Collections.Generic;
using UnityEngine;

namespace GameInventory
{
    /// <summary>
    /// 装备管理器 —— 角色身上装备槽位的增删改查与套装效果计算。
    ///
    /// 不依赖 UI，纯数据层。UI 通过 InventoryEvents 订阅刷新。
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        public static EquipmentManager Instance { get; private set; }

        private readonly Dictionary<EquipmentSlotType, EquipmentSlot> _slots = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // 初始化所有槽位
            foreach (EquipmentSlotType type in System.Enum.GetValues(typeof(EquipmentSlotType)))
                _slots[type] = new EquipmentSlot { SlotType = type };
        }

        // ==================== 装备 / 卸下 ====================

        /// <summary>装备物品到对应槽位。返回被替换的旧装备（如果有）。</summary>
        public ItemSO Equip(IEquipment item)
        {
            var slot = _slots[item.SlotType];
            var old = slot.EquippedItem?.SourceItem;
            slot.Equip(item);
            RecalculateSetBonuses();
            return old;
        }

        /// <summary>卸下指定槽位的装备。返回被卸下的物品。</summary>
        public ItemSO Unequip(EquipmentSlotType slotType)
        {
            var slot = _slots[slotType];
            var removed = slot.EquippedItem?.SourceItem;
            slot.Unequip();
            RecalculateSetBonuses();
            return removed;
        }

        // ==================== 查询 ====================

        public IEquipment GetEquipment(EquipmentSlotType slotType)
            => _slots.TryGetValue(slotType, out var slot) ? slot.EquippedItem : null;

        public bool IsEquipped(string itemId)
        {
            foreach (var kv in _slots)
                if (kv.Value.EquippedItem?.Id == itemId)
                    return true;
            return false;
        }

        /// <summary>汇总所有装备的属性加成（不含套装效果）</summary>
        public List<EquipmentEffect> GetTotalEffects()
        {
            var total = new List<EquipmentEffect>();
            foreach (var kv in _slots)
            {
                if (kv.Value.EquippedItem?.Effects == null) continue;
                total.AddRange(kv.Value.EquippedItem.Effects);
            }
            return total;
        }

        /// <summary>当前激活的套装加成</summary>
        public List<SetBonus> ActiveSetBonuses { get; private set; } = new();

        /// <summary>对比装备：返回目标装备相较当前槽位装备的属性差异</summary>
        public List<EquipmentEffect> CompareEquipment(EquipmentSlotType slotType, IEquipment newItem)
        {
            var current = GetEquipment(slotType);
            var diff = new List<EquipmentEffect>();

            var newEffects = newItem?.Effects ?? new EquipmentEffect[0];
            var curEffects = current?.Effects ?? new EquipmentEffect[0];

            // 新装备有的属性
            foreach (var e in newEffects)
            {
                int curVal = 0;
                foreach (var c in curEffects)
                    if (c.Type == e.Type) { curVal = c.Value; break; }
                if (e.Value != curVal)
                    diff.Add(new EquipmentEffect { Type = e.Type, Value = e.Value - curVal });
            }

            // 旧装备有但新装备没有的属性（卸下时显示）
            foreach (var c in curEffects)
            {
                bool found = false;
                foreach (var e in newEffects)
                    if (e.Type == c.Type) { found = true; break; }
                if (!found)
                    diff.Add(new EquipmentEffect { Type = c.Type, Value = -c.Value });
            }

            return diff;
        }

        // ==================== 套装计算 ====================

        private void RecalculateSetBonuses()
        {
            ActiveSetBonuses.Clear();

            // 统计各套装的装备件数
            var setCounts = new Dictionary<string, int>();
            var setDefinitions = new Dictionary<string, SetBonus[]>();

            foreach (var kv in _slots)
            {
                var eq = kv.Value.EquippedItem;
                if (eq == null || string.IsNullOrEmpty(eq.SetName)) continue;

                if (!setCounts.ContainsKey(eq.SetName))
                {
                    setCounts[eq.SetName] = 0;
                    setDefinitions[eq.SetName] = eq.SetBonuses;
                }
                setCounts[eq.SetName]++;
            }

            // 检查是否触发套装效果
            foreach (var kv in setCounts)
            {
                if (!setDefinitions.TryGetValue(kv.Key, out var bonuses)) continue;
                foreach (var bonus in bonuses)
                {
                    if (kv.Value >= bonus.RequiredPieces)
                        ActiveSetBonuses.Add(bonus);
                }
            }

            InventoryEvents.FireSetBonusChanged();
        }
    }
}
