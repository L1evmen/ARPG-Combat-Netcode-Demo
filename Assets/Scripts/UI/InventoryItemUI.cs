using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameUI
{
    /// <summary>
    /// 背包网格中的单个物品 UI 项。
    /// </summary>
    public class InventoryItemUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private GameObject _newBadge;
        [SerializeField] private GameObject _equippedBadge;
        [SerializeField] private Button _button;

        private ItemSlot _slot;
        private System.Action<ItemSlot> _onClick;

        // 品质边框颜色映射
        private static readonly Color[] RarityColors =
        {
            new Color(0.7f, 0.7f, 0.7f),  // Common   灰
            new Color(0.3f, 0.9f, 0.3f),  // Uncommon 绿
            new Color(0.3f, 0.5f, 1.0f),  // Rare     蓝
            new Color(0.8f, 0.3f, 1.0f),  // Epic     紫
            new Color(1.0f, 0.7f, 0.2f),  // Legendary 金
        };

        public void Initialize(ItemSlot slot, System.Action<ItemSlot> onClick)
        {
            _slot = slot;
            _onClick = onClick;

            if (_icon != null) _icon.sprite = slot.Item.Icon;
            if (_rarityBorder != null)
                _rarityBorder.color = RarityColors[(int)slot.Item.Rarity];
            if (_nameText != null) _nameText.text = slot.Item.Name;
            if (_countText != null)
            {
                _countText.text = slot.Count > 1 ? $"x{slot.Count}" : "";
                _countText.enabled = slot.Count > 1;
            }
            if (_newBadge != null)
                _newBadge.SetActive(slot.Item.IsNew);
            if (_equippedBadge != null)
                _equippedBadge.SetActive(
                    slot.Item is GameInventory.IEquipment &&
                    GameInventory.EquipmentManager.Instance.IsEquipped(slot.Item.Id));

            if (_button != null)
                _button.onClick.AddListener(() => _onClick?.Invoke(_slot));
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveAllListeners();
        }
    }
}
