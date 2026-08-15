using UnityEngine;

namespace GameInventory
{
    /// <summary>
    /// 快捷道具栏管理器 —— 4 个快捷槽位，对应方向键或 D-Pad。
    ///
    /// 消耗品放入快捷栏后可在战斗中快速使用。
    /// 不依赖 UI —— UI 通过 InventoryEvents 订阅刷新。
    /// </summary>
    public class QuickSlotManager : MonoBehaviour
    {
        public static QuickSlotManager Instance { get; private set; }

        public const int SLOT_COUNT = 4;

        private readonly QuickSlot[] _slots = new QuickSlot[SLOT_COUNT];

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < SLOT_COUNT; i++)
                _slots[i] = new QuickSlot();
        }

        private void OnEnable()
        {
            InventoryEvents.OnItemAdded += OnInventoryChanged;
            InventoryEvents.OnItemCountChanged += OnInventoryChanged;
            InventoryEvents.OnItemRemoved += OnInventoryChanged;
        }

        private void OnDisable()
        {
            InventoryEvents.OnItemAdded -= OnInventoryChanged;
            InventoryEvents.OnItemCountChanged -= OnInventoryChanged;
            InventoryEvents.OnItemRemoved -= OnInventoryChanged;
        }

        private void OnInventoryChanged(ItemSO item, int count)
        {
            // 背包变化时，刷新包含该物品的快捷栏槽位
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (_slots[i].Item != null && _slots[i].Item.id == item.id)
                {
                    _slots[i].RefreshCount();
                    InventoryEvents.FireQuickSlotCountChanged(i, _slots[i].Count);

                    if (_slots[i].Count <= 0)
                        ClearSlot(i);
                    break;
                }
            }
        }

        // ==================== 操作 ====================

        /// <summary>如果物品已在某个槽位中，刷新数量并返回 true</summary>
        public bool RefreshExistingSlot(ItemSO item)
        {
            if (item == null) return false;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (_slots[i].Item != null && _slots[i].Item.id == item.id)
                {
                    _slots[i].RefreshCount();
                    InventoryEvents.FireQuickSlotChanged(i, _slots[i].Item);
                    return true;
                }
            }
            return false;
        }

        /// <summary>将消耗品放入指定快捷槽位</summary>
        public void AssignSlot(int index, ItemSO item)
        {
            if (index < 0 || index >= SLOT_COUNT) return;
            _slots[index].Item = item;
            _slots[index].RefreshCount();
            InventoryEvents.FireQuickSlotChanged(index, item);
        }

        /// <summary>清空指定快捷槽位</summary>
        public void ClearSlot(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return;
            var removed = _slots[index].Item;
            _slots[index].Item = null;
            _slots[index].Count = 0;
            InventoryEvents.FireQuickSlotChanged(index, null);
        }

        /// <summary>使用指定槽位的道具一次。成功返回 true。</summary>
        public bool UseSlot(int index)
        {
            if (index < 0 || index >= SLOT_COUNT) return false;
            var slot = _slots[index];
            if (slot.Item == null) return false;

            bool used = InventoryService.Instance.UseItem(slot.Item.id.ToString());
            if (used)
            {
                slot.RefreshCount();
                InventoryEvents.FireQuickSlotCountChanged(index, slot.Count);

                if (slot.Count <= 0)
                    ClearSlot(index);
            }
            return used;
        }

        // ==================== 查询 ====================

        public ItemSO GetSlotItem(int index)
            => index >= 0 && index < SLOT_COUNT ? _slots[index].Item : null;

        public int GetSlotCount(int index)
            => index >= 0 && index < SLOT_COUNT ? _slots[index].Count : 0;

        public QuickSlot[] GetAllSlots() => _slots;

        // ==================== 内部类型 ====================

        public class QuickSlot
        {
            public ItemSO Item;
            public int Count;

            public bool IsEmpty => Item == null || Count <= 0;

            public void RefreshCount()
            {
                if (Item != null)
                    Count = InventoryService.Instance.GetItemCount(Item.id.ToString());
            }
        }
    }
}
