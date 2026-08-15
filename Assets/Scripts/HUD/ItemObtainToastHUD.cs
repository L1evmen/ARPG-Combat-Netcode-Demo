using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameHUD
{
    /// <summary>
    /// 获得物品提示 HUD —— 侧边滚动弹出。
    ///
    /// 多个物品排队显示，每条停留 2.5s 后自动上滑消失。
    /// 新物品带动态高亮边框，查看后消失。
    /// </summary>
    public class ItemObtainToastHUD : MonoBehaviour
    {
        [System.Serializable]
        public struct ToastSlot
        {
            public GameObject Root;
            public Image Icon;
            public Image HighlightBorder;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI CountText;
            public CanvasGroup CanvasGroup;
        }

        [SerializeField] private ToastSlot[] _slots = new ToastSlot[4];
        [SerializeField] private float _displayDuration = 2.5f;
        [SerializeField] private float _exitSlideSpeed = 200f;

        private readonly Queue<(Sprite icon, string name, int count, bool isNew)> _pending = new();
        private readonly float[] _slotTimers = new float[4];
        private readonly bool[] _slotExiting = new bool[4];

        private void Start()
        {
            InventoryEvents.OnNewItemObtained += OnNewItem;

            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Root != null)
                    _slots[i].Root.SetActive(false);
        }

        private void OnDestroy()
        {
            InventoryEvents.OnNewItemObtained -= OnNewItem;
        }

        private void OnNewItem(ItemSO item)
        {
            _pending.Enqueue((item.icon, item.name, 1, true));
        }

        private void Update()
        {
            // 尝试将队列中的物品放入空槽位
            while (_pending.Count > 0 && TryClaimSlot(out int freeSlot))
            {
                var item = _pending.Dequeue();
                ShowInSlot(freeSlot, item.icon, item.name, item.count, item.isNew);
            }

            // 更新各槽位计时
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slotExiting[i] && _slots[i].Root != null && _slots[i].Root.activeSelf)
                {
                    _slotTimers[i] -= Time.deltaTime;
                    if (_slotTimers[i] <= 0f)
                        _slotExiting[i] = true;
                }

                if (_slotExiting[i])
                {
                    var cg = _slots[i].CanvasGroup;
                    if (cg != null)
                    {
                        cg.alpha -= Time.deltaTime * 2f;
                        if (cg.alpha <= 0f)
                        {
                            _slots[i].Root.SetActive(false);
                            _slotExiting[i] = false;
                        }
                    }
                }
            }
        }

        private bool TryClaimSlot(out int index)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Root == null || !_slots[i].Root.activeSelf)
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        private void ShowInSlot(int i, Sprite icon, string name, int count, bool isNew)
        {
            var slot = _slots[i];
            if (slot.Root == null) return;
            slot.Root.SetActive(true);
            if (slot.Icon != null) slot.Icon.sprite = icon;
            if (slot.NameText != null) slot.NameText.text = name;
            if (slot.CountText != null) slot.CountText.text = $"x{count}";
            if (slot.HighlightBorder != null) slot.HighlightBorder.enabled = isNew;
            if (slot.CanvasGroup != null) slot.CanvasGroup.alpha = 1f;

            _slotTimers[i] = _displayDuration;
            _slotExiting[i] = false;
        }
    }
}
