using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameUI
{
    /// <summary>
    /// 物品详情面板 View —— 纯渲染，不写 Model。
    ///
    /// 功能：属性数值、对比差异（绿增/红减）、背景描述、
    /// 装备/卸下按钮。
    ///
    /// 所有用户操作委托给 InventoryController。
    /// </summary>
    public class ItemDetailPanel : MonoBehaviour
    {
        [Header("基础信息")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _rarityText;
        [SerializeField] private TextMeshProUGUI _categoryText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _flavorText;

        [Header("属性列表")]
        [SerializeField] private Transform _propertyGrid;
        [SerializeField] private PropertyRowUI _propertyRowPrefab;
        private readonly List<PropertyRowUI> _propertyRows = new();

        [Header("按钮")]
        [SerializeField] private Button _equipUseButton;
        [SerializeField] private Button _equippedButton;
        [SerializeField] private Button _unequipButton;


        private ItemSlot _currentSlot;

        private InventoryController Ctrl => InventoryController.Instance;

        private void Start()
        {
            gameObject.SetActive(false);

            if (_equipUseButton != null) _equipUseButton.onClick.AddListener(OnEquipUse);
            if (_unequipButton != null) _unequipButton.onClick.AddListener(OnUnequip);

            InventoryEvents.OnEquipmentChanged += OnEquipmentStateChanged;
            InventoryEvents.OnEquipmentRemoved += OnEquipmentStateChanged;
        }

        private void OnDestroy()
        {
            InventoryEvents.OnEquipmentChanged -= OnEquipmentStateChanged;
            InventoryEvents.OnEquipmentRemoved -= OnEquipmentStateChanged;
        }

        private void OnEquipmentStateChanged(EquipmentSlotType type, ItemSO item)
        {
            if (_currentSlot != null && _currentSlot.Item is IEquipment eq && eq.SlotType == type)
                Show(_currentSlot);
        }

        // ==================== 渲染 ====================

        public void Show(ItemSlot slot)
        {
            if (slot == null || slot.Item == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            _currentSlot = slot;

            var item = slot.Item;

            if (_icon != null) _icon.sprite = item.Icon;
            if (_nameText != null) _nameText.text = item.Name;
            if (_rarityText != null) _rarityText.text = item.Rarity.ToString();
            if (_categoryText != null) _categoryText.text = item.Category.ToString();
            if (_descriptionText != null) _descriptionText.text = item.Description;

            if (item is IEquipment equipment)
            {
                BuildPropertyRows(equipment);
                if (_flavorText != null) _flavorText.text = equipment.FlavorText;

                // 对比当前装备
                var diff = Ctrl.CompareEquipment(equipment.SlotType, equipment);
                foreach (var row in _propertyRows)
                {
                    foreach (var d in diff)
                    {
                        if (d.Type == row.PropertyType)
                        {
                            row.SetDiff(d.Value);
                            break;
                        }
                    }
                }

                bool equipped = Ctrl.IsEquipped(item.Id);
                SetEquipButtonState(equipped);
            }
            else
            {
                BuildPropertyRowsFromConsumable(item);
                if (_flavorText != null) _flavorText.text = "";

                // 消耗品：检查是否已在快捷栏中
                bool inQuickSlot = IsInQuickSlot(item.SourceItem);
                SetEquipButtonState(inQuickSlot);
            }

            // 卸下按钮：仅装备中的武器显示
            if (_unequipButton != null)
                _unequipButton.gameObject.SetActive(
                    item is IEquipment && Ctrl.IsEquipped(item.Id));
        }

        public void Clear()
        {
            _currentSlot = null;
            ClearPropertyRows();
        }

        private void BuildPropertyRows(IEquipment equipment)
        {
            ClearPropertyRows();

            if (equipment.Effects == null) return;
            foreach (var effect in equipment.Effects)
            {
                var row = Instantiate(_propertyRowPrefab, _propertyGrid);
                row.transform.SetAsLastSibling();
                row.Set(effect.Type, effect.Value);
                _propertyRows.Add(row);
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_propertyGrid as RectTransform);
        }

        private void BuildPropertyRowsFromConsumable(IInventoryItem item)
        {
            ClearPropertyRows();

            if (item is IConsumable consumable && consumable.InstantEffects != null)
            {
                foreach (var effect in consumable.InstantEffects)
                {
                    var row = Instantiate(_propertyRowPrefab, _propertyGrid);
                    row.transform.SetAsLastSibling();
                    row.Set(effect.Type, effect.Value);
                    _propertyRows.Add(row);
                }
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_propertyGrid as RectTransform);
            }
        }

        private void ClearPropertyRows()
        {
            foreach (var row in _propertyRows)
                if (row != null) Destroy(row.gameObject);
            _propertyRows.Clear();
        }

        private void SetEquipButtonState(bool equipped)
        {
            if (_equipUseButton != null) _equipUseButton.gameObject.SetActive(!equipped);
            if (_equippedButton != null) _equippedButton.gameObject.SetActive(equipped);
        }

        /// <summary>检查物品是否已在快捷栏任一槽位中</summary>
        private bool IsInQuickSlot(ItemSO item)
        {
            if (QuickSlotManager.Instance == null || item == null) return false;
            for (int i = 0; i < QuickSlotManager.SLOT_COUNT; i++)
                if (QuickSlotManager.Instance.GetSlotItem(i) == item)
                    return true;
            return false;
        }

        // ==================== 按钮回调 ====================

        public void OnEquipUse()
        {
            if (_currentSlot?.Item == null) return;

            if (_currentSlot.Item.SourceItem.itemType == ItemType.Weapon
                && _currentSlot.Item is IEquipment equipment)
            {
                Ctrl.EquipItem(equipment);
            }
            else
            {
                // 消耗品：装备到快捷栏第一个空槽位
                var item = _currentSlot.Item.SourceItem;
                if (item != null)
                    Ctrl.AssignToFirstEmptyQuickSlot(item);
            }

            // 切换按钮显示
            SetEquipButtonState(true);
        }

        public void OnUnequip()
        {
            if (_currentSlot?.Item is IEquipment equipment)
            {
                Ctrl.UnequipItem(equipment.SlotType);
                Clear();
            }
        }
    }
}
