using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameUI
{
    /// <summary>
    /// 快捷栏配置面板 —— 背包底部，用于将消耗品拖拽/指派到快捷槽位。
    /// </summary>
    public class QuickSlotConfigPanel : MonoBehaviour
    {
        [SerializeField] private QuickSlotConfigUI[] _slots = new QuickSlotConfigUI[QuickSlotManager.SLOT_COUNT];

        private void Start()
        {
            InventoryEvents.OnQuickSlotChanged += OnSlotChanged;
            RefreshAll();
        }

        private void OnDestroy()
        {
            InventoryEvents.OnQuickSlotChanged -= OnSlotChanged;
        }

        private void OnSlotChanged(int index, ItemSO item)
        {
            if (index >= 0 && index < _slots.Length)
                RefreshSlot(index);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _slots.Length; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            var ui = _slots[index];
            var item = QuickSlotManager.Instance.GetSlotItem(index);
            int count = QuickSlotManager.Instance.GetSlotCount(index);

            if (ui.Icon != null)
            {
                ui.Icon.sprite = item != null ? item.icon : null;
                ui.Icon.enabled = item != null;
            }
            if (ui.NameText != null)
                ui.NameText.text = item != null ? item.name : "空";
            if (ui.CountText != null)
                ui.CountText.text = count > 0 ? $"x{count}" : "";
            if (ui.KeyHint != null && index < KEY_HINTS.Length)
                ui.KeyHint.text = KEY_HINTS[index];
        }

        private static readonly string[] KEY_HINTS = { "↑", "↓", "←", "→" };
    }

    [System.Serializable]
    public struct QuickSlotConfigUI
    {
        public Image Icon;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI CountText;
        public TextMeshProUGUI KeyHint;
    }
}
