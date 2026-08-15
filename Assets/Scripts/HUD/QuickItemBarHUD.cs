using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameHUD
{
    /// <summary>
    /// 恢复品快捷栏 HUD —— 屏幕右下方常驻。
    ///
    /// 显示 4 个快捷槽位：上(↑)、下(↓)、左(←)、右(→) 或 D-Pad 对应。
    /// 每个槽位显示物品图标 + 数量（如 "5/8"）+ 对应按键提示。
    /// 治疗葫芦/核心恢复品在第一个槽位高亮。
    ///
    /// 布局（16:9，锚定右下角，偏移 -60, 60）：
    ///          [↑] 图标 x5/8
    ///   [←] 图标 x3    [→] 图标 x2
    ///          [↓] 图标 x7/10
    /// </summary>
    public class QuickItemBarHUD : MonoBehaviour
    {
        [System.Serializable]
        public struct QuickSlotUI
        {
            public Image Icon;
            public Image IconBg;
            public TextMeshProUGUI CountText;
            public TextMeshProUGUI KeyHint;       // ↑ ↓ ← →
            public GameObject EmptyState;
        }

        [SerializeField] private QuickSlotUI[] _slots = new QuickSlotUI[QuickSlotManager.SLOT_COUNT];

        private void Start()
        {
            UpdateAllSlots();

            // 订阅快捷栏变化事件
            InventoryEvents.OnQuickSlotChanged += OnSlotChanged;
            InventoryEvents.OnQuickSlotCountChanged += OnCountChanged;
        }

        private void OnDestroy()
        {
            InventoryEvents.OnQuickSlotChanged -= OnSlotChanged;
            InventoryEvents.OnQuickSlotCountChanged -= OnCountChanged;
        }

        private void OnSlotChanged(int index, ItemSO item)
        {
            if (index < 0 || index >= _slots.Length) return;
            UpdateSlot(index);
        }

        private void OnCountChanged(int index, int count)
        {
            if (index < 0 || index >= _slots.Length) return;
            var slot = _slots[index];
            if (slot.CountText != null)
                slot.CountText.text = count > 0 ? count.ToString() : "";
        }

        private void UpdateAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                UpdateSlot(i);
        }

        private void UpdateSlot(int index)
        {
            var ui = _slots[index];
            var item = QuickSlotManager.Instance.GetSlotItem(index);
            int count = QuickSlotManager.Instance.GetSlotCount(index);
            bool hasItem = item != null && count > 0;

            if (ui.Icon != null)
            {
                ui.Icon.sprite = hasItem ? item.icon : null;
                ui.Icon.enabled = hasItem;
            }
            if (ui.EmptyState != null)
                ui.EmptyState.SetActive(!hasItem);
            if (ui.CountText != null)
                ui.CountText.text = count > 0 ? count.ToString() : "";
            if (ui.IconBg != null)
                ui.IconBg.color = index == 0 ? new Color(1f, 0.85f, 0.3f, 0.3f) : Color.clear;
        }
    }
}
