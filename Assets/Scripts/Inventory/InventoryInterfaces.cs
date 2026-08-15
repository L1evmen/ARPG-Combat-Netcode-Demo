using System.Collections.Generic;
using UnityEngine;

namespace GameInventory
{
    /// <summary>
    /// 装备效果 —— 装备提供的属性加成。
    /// </summary>
    public struct EquipmentEffect
    {
        public PropertyType Type;
        public int Value;
    }

    /// <summary>
    /// 套装效果定义 —— 凑齐 N 件触发对应加成。
    /// </summary>
    public struct SetBonus
    {
        public int RequiredPieces;              // 需要件数
        public EquipmentEffect[] Effects;       // 触发效果
        public string Description;              // 效果描述
    }

    /// <summary>
    /// 物品接口 —— 所有物品类型必须实现。
    /// 已有 ItemSO 通过适配器实现此接口，避免重构旧代码。
    /// </summary>
    public interface IInventoryItem
    {
        string Id { get; }
        string Name { get; }
        ItemCategory Category { get; }
        ItemRarity Rarity { get; }
        string Description { get; }
        Sprite Icon { get; }
        int MaxStack { get; }           // 最大堆叠数（1 = 不可堆叠）
        bool IsNew { get; set; }        // 是否为新获得物品
        int AcquisitionTime { get; }    // 获得时间戳（用于排序）
        ItemSO SourceItem { get; }      // 原始 ItemSO 引用（用于事件广播与旧系统交互）
    }

    /// <summary>
    /// 可装备物品接口。
    /// </summary>
    public interface IEquipment : IInventoryItem
    {
        EquipmentSlotType SlotType { get; }
        EquipmentEffect[] Effects { get; }
        string SetName { get; }                   // 套装名（空 = 不属于套装）
        SetBonus[] SetBonuses { get; }
        string FlavorText { get; }                // 背景描述
        string SpecialEffect { get; }             // 特殊效果描述
        bool CanUpgrade { get; }
    }

    /// <summary>
    /// 可消耗物品接口。
    /// </summary>
    public interface IConsumable : IInventoryItem
    {
        ConsumableType ConsumableType { get; }
        EquipmentEffect[] InstantEffects { get; }  // 使用后生效的效果
        float CooldownSeconds { get; }
    }

    public enum ConsumableType
    {
        Recovery,     // 恢复品（葫芦/丹药）
        Buff,         // 增益道具
        Throwable,    // 投掷物
        Material,     // 材料
        QuestItem     // 任务物品
    }

    /// <summary>
    /// 精魄接口（提供变身或特殊技能）。
    /// </summary>
    public interface ISpirit : IInventoryItem
    {
        string TransformationName { get; }
        string SkillName { get; }
        string SkillDescription { get; }
        float CooldownSeconds { get; }
        float DurationSeconds { get; }
        EquipmentEffect[] PassiveEffects { get; }
    }

    /// <summary>
    /// 法宝接口（主动技能装备）。
    /// </summary>
    public interface ITreasure : IInventoryItem
    {
        string SkillName { get; }
        string SkillDescription { get; }
        float CooldownSeconds { get; }
        EquipmentEffect[] PassiveEffects { get; }
    }

    /// <summary>
    /// 物品槽位数据 —— 存储物品引用 + 数量。
    /// </summary>
    public class ItemSlot
    {
        public IInventoryItem Item;
        public int Count;

        public bool IsEmpty => Item == null || Count <= 0;
        public bool IsFull => Item != null && Count >= Item.MaxStack;

        public void Clear()
        {
            Item = null;
            Count = 0;
        }
    }

    /// <summary>
    /// 装备槽位数据。
    /// </summary>
    public class EquipmentSlot
    {
        public EquipmentSlotType SlotType;
        public IEquipment EquippedItem;

        public bool IsEmpty => EquippedItem == null;
        public EquipmentEffect[] ActiveEffects => EquippedItem?.Effects;

        public void Equip(IEquipment item)
        {
            EquippedItem = item;
            InventoryEvents.FireEquipmentChanged(SlotType, item.SourceItem);
        }

        public void Unequip()
        {
            var removed = EquippedItem;
            EquippedItem = null;
            if (removed != null)
                InventoryEvents.FireEquipmentRemoved(SlotType, removed.SourceItem);
        }
    }
}
