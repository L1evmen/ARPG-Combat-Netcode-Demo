using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameInventory
{
    /// <summary>
    /// 背包数据服务 —— 管理所有物品的增删改查、排序、筛选。
    ///
    /// 使用方式：挂载到 Persistence 对象上，作为全局单例。
    /// 所有物品操作通过此服务进行，UI 通过事件订阅刷新。
    /// </summary>
    public class InventoryService : MonoBehaviour
    {
        public static InventoryService Instance { get; private set; }

        [Header("容量")]
        [SerializeField] private int _maxCapacity = 99;
        public int MaxCapacity => _maxCapacity;
        public int CurrentCount => _slots.Count(s => !s.IsEmpty);
        public bool IsFull => CurrentCount >= _maxCapacity;

        private readonly List<ItemSlot> _slots = new List<ItemSlot>();

        // ---- 排序/筛选状态（由 UI 设置） ----
        public ItemCategory ActiveCategory { get; set; } = ItemCategory.All;
        public SortMode ActiveSortMode { get; set; } = SortMode.AcquisitionTime;
        public bool SortDescending { get; set; } = true;

        public enum SortMode
        {
            AcquisitionTime,  // 获得时间
            Name,             // 名称
            Rarity,           // 品质
            Count,            // 数量
            Type              // 类型
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ==================== 增删改 ====================

        /// <summary>添加物品。自动堆叠或创建新槽位。返回成功添加的数量。</summary>
        public int AddItem(ItemSO item, int count = 1)
        {
            if (item == null || count <= 0) return 0;

            int added = 0;
            int maxStack = item.itemType == ItemType.Consumable ? 99 : 1;

            // 先尝试堆叠到已有槽位
            foreach (var slot in _slots)
            {
                if (slot.Item == null) continue;
                if (slot.Item.Id != item.id.ToString()) continue;
                if (slot.IsFull) continue;

                int canAdd = Mathf.Min(count - added, maxStack - slot.Count);
                slot.Count += canAdd;
                added += canAdd;
                InventoryEvents.FireItemCountChanged(item, slot.Count);

                if (added >= count) return added;
            }

            // 剩余创建新槽位
            while (added < count && !IsFull)
            {
                int canAdd = Mathf.Min(count - added, maxStack);
                var newSlot = new ItemSlot
                {
                    Item = new ItemAdapter(item, Time.frameCount),
                    Count = canAdd
                };
                _slots.Add(newSlot);
                added += canAdd;
                InventoryEvents.FireItemAdded(item, canAdd);
                InventoryEvents.FireNewItemObtained(item);
            }

            if (added < count)
                Debug.LogWarning($"[Inventory] 背包已满，未能全部放入 '{item.name}'");

            return added;
        }

        /// <summary>移除物品。返回成功移除的数量。</summary>
        public int RemoveItem(string itemId, int count = 1)
        {
            int removed = 0;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].Item?.Id != itemId) continue;

                int canRemove = Mathf.Min(count - removed, _slots[i].Count);
                _slots[i].Count -= canRemove;
                removed += canRemove;

                if (_slots[i].Count <= 0)
                {
                    var item = _slots[i].Item.SourceItem;
                    _slots.RemoveAt(i);
                    if (item != null)
                        InventoryEvents.FireItemRemoved(item, 0);
                }
                else
                {
                    InventoryEvents.FireItemCountChanged(_slots[i].Item.SourceItem, _slots[i].Count);
                }

                if (removed >= count) break;
            }
            return removed;
        }

        /// <summary>使用一个消耗品（减少1个数量）</summary>
        public bool UseItem(string itemId)
        {
            return RemoveItem(itemId, 1) > 0;
        }

        // ==================== 查询 ====================

        public int GetItemCount(string itemId)
        {
            int total = 0;
            foreach (var slot in _slots)
                if (slot.Item?.Id == itemId)
                    total += slot.Count;
            return total;
        }

        public bool HasItem(string itemId, int count = 1)
            => GetItemCount(itemId) >= count;

        // ==================== ItemSO 便利重载 ====================

        /// <summary>移除物品（ItemSO 重载）</summary>
        public int RemoveItem(ItemSO item, int count = 1)
            => item != null ? RemoveItem(item.id.ToString(), count) : 0;

        /// <summary>获取物品数量（ItemSO 重载）</summary>
        public int GetItemCount(ItemSO item)
            => item != null ? GetItemCount(item.id.ToString()) : 0;

        /// <summary>获取所有物品的 ItemSO 列表</summary>
        public List<ItemSO> GetAllItemSOs()
            => _slots.Where(s => !s.IsEmpty && s.Item?.SourceItem != null)
                     .Select(s => s.Item.SourceItem).ToList();

        /// <summary>获取所有武器类物品</summary>
        public List<ItemSO> GetWeapons()
            => GetAllItemSOs().Where(i => i.itemType == ItemType.Weapon).ToList();

        /// <summary>获取所有消耗品类物品</summary>
        public List<ItemSO> GetConsumables()
            => GetAllItemSOs().Where(i => i.itemType == ItemType.Consumable).ToList();

        /// <summary>获取排序筛选项后的物品列表（UI 绑定）</summary>
        public List<ItemSlot> GetFilteredItems()
        {
            IEnumerable<ItemSlot> query = _slots;

            // 分类筛选
            if (ActiveCategory != ItemCategory.All)
                query = query.Where(s => s.Item?.Category == ActiveCategory);

            // 排序
            query = ActiveSortMode switch
            {
                SortMode.Name => SortDescending
                    ? query.OrderByDescending(s => s.Item?.Name)
                    : query.OrderBy(s => s.Item?.Name),

                SortMode.Rarity => SortDescending
                    ? query.OrderByDescending(s => s.Item?.Rarity)
                    : query.OrderBy(s => s.Item?.Rarity),

                SortMode.Count => SortDescending
                    ? query.OrderByDescending(s => s.Count)
                    : query.OrderBy(s => s.Count),

                SortMode.Type => SortDescending
                    ? query.OrderByDescending(s => s.Item?.Category)
                    : query.OrderBy(s => s.Item?.Category),

                _ => SortDescending  // AcquisitionTime
                    ? query.OrderByDescending(s => s.Item?.AcquisitionTime)
                    : query.OrderBy(s => s.Item?.AcquisitionTime)
            };

            return query.ToList();
        }

        /// <summary>标记所有物品为已查看（清除"新物品"高亮）</summary>
        public void MarkAllAsViewed()
        {
            foreach (var slot in _slots)
                if (slot.Item != null)
                    slot.Item.IsNew = false;
        }
    }

    /// <summary>
    /// ItemSO → IInventoryItem 适配器。
    /// 避免修改旧 ItemSO，通过适配器满足 IInventoryItem 协议。
    /// </summary>
    internal class ItemAdapter : IInventoryItem, IEquipment, IConsumable
    {
        private readonly ItemSO _item;

        public ItemAdapter(ItemSO item, int acquisitionTime)
        {
            _item = item;
            AcquisitionTime = acquisitionTime;
            IsNew = true;
        }

        // ---- IInventoryItem ----
        public string Id => _item.id.ToString();
        public string Name => _item.name;
        public ItemCategory Category => MapCategory(_item.itemType);
        public ItemRarity Rarity => ItemRarity.Common;
        public string Description => _item.description;
        public Sprite Icon => _item.icon;
        public int MaxStack => _item.itemType == ItemType.Consumable ? 99 : 1;
        public bool IsNew { get; set; }
        public int AcquisitionTime { get; }
        public ItemSO SourceItem => _item;

        // ---- IEquipment（仅当 itemType == Weapon 时有效） ----
        public EquipmentSlotType SlotType => EquipmentSlotType.Weapon;
        public EquipmentEffect[] Effects
        {
            get
            {
                if (_item.propertyList == null || _item.propertyList.Count == 0)
                    return new EquipmentEffect[0];
                var list = new EquipmentEffect[_item.propertyList.Count];
                for (int i = 0; i < _item.propertyList.Count; i++)
                    list[i] = new EquipmentEffect { Type = _item.propertyList[i].propertyType, Value = _item.propertyList[i].value };
                return list;
            }
        }
        public string SetName => "";
        public SetBonus[] SetBonuses => null;
        public string FlavorText => "";
        public string SpecialEffect => "";
        public bool CanUpgrade => false;

        // ---- IConsumable（仅当 itemType == Consumable 时有效） ----
        public ConsumableType ConsumableType => MapConsumableType(_item);
        public EquipmentEffect[] InstantEffects => Effects;
        public float CooldownSeconds => 0f;

        private static ItemCategory MapCategory(ItemType type) => type switch
        {
            ItemType.Weapon => ItemCategory.Weapon,
            ItemType.Consumable => ItemCategory.Consumable,
            _ => ItemCategory.Material
        };

        private static ConsumableType MapConsumableType(ItemSO item)
        {
            if (item.itemType != ItemType.Consumable) return ConsumableType.Material;
            // 有 HP/Energy 恢复属性的为恢复品
            if (item.propertyList != null)
                foreach (var p in item.propertyList)
                    if (p.propertyType == PropertyType.HPValue || p.propertyType == PropertyType.EnergyValue)
                        return ConsumableType.Recovery;
            return ConsumableType.Buff;
        }
    }
}
