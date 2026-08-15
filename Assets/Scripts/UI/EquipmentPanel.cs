using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameUI
{
    /// <summary>
    /// 装备栏面板 View —— 纯渲染，不写 Model。
    ///
    /// 包含：武器、头部、衣甲、臂甲、腿甲、饰品×2、精魄、法宝。
    /// 支持单击卸下。
    ///
    /// 所有用户操作委托给 InventoryController。
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        [System.Serializable]
        public struct EquipmentSlotUI
        {
            public EquipmentSlotType SlotType;
            public Image Icon;
            public Image EmptyIcon;
            public TextMeshProUGUI SlotName;
            public Button SlotButton;
            public Image HighlightBorder;
        }

        [SerializeField] private EquipmentSlotUI[] _slots;

        private InventoryController Ctrl => InventoryController.Instance;

        private void Start()
        {
            foreach (var slot in _slots)
            {
                if (slot.SlotButton != null)
                {
                    var type = slot.SlotType;
                    slot.SlotButton.onClick.AddListener(() => OnSlotClicked(type));
                }
            }

            InventoryEvents.OnEquipmentChanged += OnEquipmentChanged;
            InventoryEvents.OnEquipmentRemoved += OnEquipmentRemoved;
        }

        private void OnDestroy()
        {
            InventoryEvents.OnEquipmentChanged -= OnEquipmentChanged;
            InventoryEvents.OnEquipmentRemoved -= OnEquipmentRemoved;
        }

        private void OnEquipmentChanged(EquipmentSlotType type, ItemSO item)
        {
            foreach (var slot in _slots)
            {
                if (slot.SlotType != type) continue;
                if (slot.Icon != null)
                {
                    slot.Icon.sprite = item.icon;
                    slot.Icon.enabled = true;
                }
                if (slot.EmptyIcon != null)
                    slot.EmptyIcon.gameObject.SetActive(false);
                break;
            }
        }

        private void OnEquipmentRemoved(EquipmentSlotType type, ItemSO item)
        {
            foreach (var slot in _slots)
            {
                if (slot.SlotType != type) continue;
                if (slot.Icon != null)
                    slot.Icon.enabled = false;
                if (slot.EmptyIcon != null)
                    slot.EmptyIcon.gameObject.SetActive(true);
                break;
            }
        }

        private void OnSlotClicked(EquipmentSlotType type)
        {
            var equipped = EquipmentManager.Instance.GetEquipment(type);
            if (equipped != null)
                Ctrl.UnequipItem(type);
        }
    }
}
