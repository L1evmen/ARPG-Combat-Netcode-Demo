using System;
using UnityEngine;

namespace GameInventory
{
    /// <summary>
    /// MVC Controller —— 背包系统唯一写入入口。
    ///
    /// 职责：
    /// 1. 接收 View 的用户操作委托
    /// 2. 调用 Model 层方法
    /// 3. 处理输入（Tab 键切换背包）
    /// 4. 操作后通知 View 刷新
    ///
    /// View 绝不直接写 Model —— 所有写操作必须经过 Controller。
    /// </summary>
    public class InventoryController : MonoBehaviour
    {
        public static InventoryController Instance { get; private set; }

        [Header("View 引用")]
        [SerializeField] private GameUI.InventoryPanel _inventoryPanel;
        [SerializeField] private GameUI.ItemDetailPanel _detailPanel;

        [Header("3D 角色预览")]
        [SerializeField] private GameUI.CharacterPreviewController _characterPreview;

        public bool IsOpen { get; private set; }

        public event Action OnOpened;
        public event Action OnClosed;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                Toggle();
        }

        // ==================== 背包开关 ====================

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            _characterPreview?.Enable();

            _inventoryPanel?.Show();
            OnOpened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            _characterPreview?.Disable();

            InventoryService.Instance.MarkAllAsViewed();
            _detailPanel?.Clear();
            _inventoryPanel?.Hide();
            OnClosed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        // ==================== 筛选 / 排序 ====================

        public void SelectCategory(ItemCategory category)
        {
            InventoryService.Instance.ActiveCategory = category;
            _inventoryPanel?.RefreshItemGrid();
        }

        public void SetSortMode(InventoryService.SortMode mode)
        {
            InventoryService.Instance.ActiveSortMode = mode;
            _inventoryPanel?.RefreshItemGrid();
        }

        public void SetSortDescending(bool descending)
        {
            InventoryService.Instance.SortDescending = descending;
            _inventoryPanel?.RefreshItemGrid();
        }

        // ==================== 物品操作 ====================

        public bool UseItem(string itemId, ItemSO item)
        {
            bool used = InventoryService.Instance.UseItem(itemId);
            if (used)
                _inventoryPanel?.RefreshItemGrid();
            return used;
        }

        // ==================== 装备操作 ====================

        /// <summary>
        /// 装备物品。物品从背包移除，显示在装备栏。
        /// 返回被替换的旧装备 ItemSO。
        /// </summary>
        public ItemSO EquipItem(IEquipment equipment)
        {
            if (EquipmentManager.Instance.IsEquipped(equipment.Id))
                return null;

            var old = EquipmentManager.Instance.Equip(equipment);
            InventoryService.Instance.RemoveItem(equipment.SourceItem);

            // 同步 WeaponManager（实际切换玩家手中的武器模型）
            if (equipment.SlotType == EquipmentSlotType.Weapon && WeaponManager.Instance != null)
                WeaponManager.Instance.EquipToNextSlot(equipment.SourceItem);

            if (old != null)
            {
                // 被替换的旧武器从 WeaponManager 中移除
                if (WeaponManager.Instance != null)
                {
                    for (int i = 0; i < WeaponManager.Instance.slots.Length; i++)
                    {
                        if (WeaponManager.Instance.slots[i] == old)
                        {
                            WeaponManager.Instance.ClearSlot(i);
                            break;
                        }
                    }
                }

                InventoryService.Instance.AddItem(old);
            }

            _inventoryPanel?.RefreshItemGrid();
            return old;
        }

        /// <summary>卸下指定槽位的装备。</summary>
        public ItemSO UnequipItem(EquipmentSlotType slotType)
        {
            var item = EquipmentManager.Instance.Unequip(slotType);
            if (item != null)
            {
                // 同步 WeaponManager（从武器槽中移除）
                if (slotType == EquipmentSlotType.Weapon && WeaponManager.Instance != null)
                {
                    for (int i = 0; i < WeaponManager.Instance.slots.Length; i++)
                    {
                        if (WeaponManager.Instance.slots[i] == item)
                        {
                            WeaponManager.Instance.ClearSlot(i);
                            break;
                        }
                    }
                }

                InventoryService.Instance.AddItem(item);
            }
            _inventoryPanel?.RefreshItemGrid();
            return item;
        }

        public System.Collections.Generic.List<EquipmentEffect> CompareEquipment(
            EquipmentSlotType slotType, IEquipment newItem)
        {
            return EquipmentManager.Instance.CompareEquipment(slotType, newItem);
        }

        public bool IsEquipped(string itemId)
        {
            return EquipmentManager.Instance.IsEquipped(itemId);
        }

        // ==================== 快捷栏操作 ====================

        public void AssignQuickSlot(int index, ItemSO item)
        {
            QuickSlotManager.Instance.AssignSlot(index, item);
        }

        public void AssignToFirstEmptyQuickSlot(ItemSO item)
        {
            // 相同物品已在快捷栏中 → 只刷新数量
            if (QuickSlotManager.Instance.RefreshExistingSlot(item))
                return;

            // 否则放入第一个空位
            for (int i = 0; i < QuickSlotManager.SLOT_COUNT; i++)
            {
                if (QuickSlotManager.Instance.GetSlotItem(i) == null)
                {
                    QuickSlotManager.Instance.AssignSlot(i, item);
                    return;
                }
            }
        }

        // ==================== 选中物品（详情面板） ====================

        public void SelectItem(ItemSlot slot)
        {
            _detailPanel?.Show(slot);
        }
    }
}
