using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameInventory;

namespace GameUI
{
    /// <summary>
    /// 背包主面板 View —— 纯渲染，不写 Model。
    ///
    /// 布局（16:9 居中，占屏幕 85%）：
    ///   ┌──────────────────────────────────────────────┐
    ///   │  Tab: [全部][武器][防具][消耗品][材料][任务][精魄][法宝]  │
    ///   │  ────────────────────────────────────────────│
    ///   │  左侧: 角色模型/立绘 + 装备槽位              │
    ///   │  中央: 物品列表（ScrollView Grid）            │
    ///   │  右侧: 选中物品详情 + 操作按钮               │
    ///   │  底部: 快捷道具栏 + 排序/筛选按钮            │
    ///   └──────────────────────────────────────────────┘
    ///
    /// 用户操作委托给 InventoryController，绝对不直接写 Model。
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        public static InventoryPanel Instance { get; private set; }

        [Header("根节点")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("分类标签")]
        [SerializeField] private TabButton[] _categoryTabs;

        [Header("物品列表")]
        [SerializeField] private Transform _itemGridContent;
        [SerializeField] private InventoryItemUI _itemPrefab;
        private readonly List<InventoryItemUI> _spawnedItems = new();

        [Header("排序/筛选")]
        [SerializeField] private TMP_Dropdown _sortDropdown;
        [SerializeField] private Toggle _sortDescToggle;

        [Header("子面板")]
        [SerializeField] private EquipmentPanel _equipmentPanel;
        [SerializeField] private ItemDetailPanel _detailPanel;
        [SerializeField] private QuickSlotConfigPanel _quickSlotPanel;

        [Header("3D 角色预览")]
        [SerializeField] private RawImage _characterPreviewImage;

        [Header("动画")]
        [SerializeField] private float _openAnimDuration = 0.2f;
        [SerializeField] private AnimationCurve _openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private bool _isOpen;
        private float _animTimer;

        public bool IsOpen => _isOpen;

        private InventoryController Ctrl => InventoryController.Instance;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Tab 按钮绑定
            if (_categoryTabs != null)
            {
                for (int i = 0; i < _categoryTabs.Length; i++)
                {
                    int idx = i;
                    _categoryTabs[i].Initialize();
                    _categoryTabs[i].OnClick += () => Ctrl.SelectCategory((ItemCategory)idx);
                }
            }

            if (_sortDropdown != null)
            {
                _sortDropdown.ClearOptions();
                _sortDropdown.AddOptions(new List<string> { "获得时间", "名称", "品质", "数量", "类型" });
                _sortDropdown.onValueChanged.AddListener(v => Ctrl.SetSortMode((InventoryService.SortMode)v));
            }

            if (_sortDescToggle != null)
                _sortDescToggle.onValueChanged.AddListener(v => Ctrl.SetSortDescending(v));

            // 订阅 Model 事件自动刷新
            InventoryEvents.OnItemAdded += OnModelItemChanged;
            InventoryEvents.OnItemRemoved += OnModelItemChanged;
            InventoryEvents.OnItemCountChanged += OnModelItemCountChanged;

            HideImmediate();
        }

        private void Update()
        {
            if (_canvasGroup != null)
            {
                _animTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_animTimer / _openAnimDuration);
                _canvasGroup.alpha = _isOpen
                    ? _openCurve.Evaluate(t)
                    : 1f - _openCurve.Evaluate(t);

                if (t >= 1f && !_isOpen)
                    _panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            InventoryEvents.OnItemAdded -= OnModelItemChanged;
            InventoryEvents.OnItemRemoved -= OnModelItemChanged;
            InventoryEvents.OnItemCountChanged -= OnModelItemCountChanged;
        }

        // ==================== 显示/隐藏（由 Controller 调用） ====================

        public void Show()
        {
            _isOpen = true;
            _animTimer = 0f;
            _panelRoot.SetActive(true);
            RefreshItemGrid();
            UpdateTabHighlights();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var anim = GetComponentInParent<Animator>();
            if (anim != null) anim.SetBool("IsUiOpen", true);
        }

        public void Hide()
        {
            _isOpen = false;
            _animTimer = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var anim = GetComponentInParent<Animator>();
            if (anim != null) anim.SetBool("IsUiOpen", false);
        }

        private void HideImmediate()
        {
            _isOpen = false;
            _panelRoot.SetActive(false);
        }

        // ==================== 物品列表刷新（对象池，无 GC） ====================

        public void RefreshItemGrid()
        {
            UpdateTabHighlights();
            var items = InventoryService.Instance.GetFilteredItems();

            while (_spawnedItems.Count < items.Count)
                _spawnedItems.Add(Instantiate(_itemPrefab, _itemGridContent));

            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (i < items.Count)
                {
                    _spawnedItems[i].Initialize(items[i], OnItemSelected);
                    _spawnedItems[i].gameObject.SetActive(true);
                }
                else
                {
                    _spawnedItems[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnItemSelected(ItemSlot slot)
        {
            Ctrl.SelectItem(slot);
        }

        // ==================== 标签高亮 ====================

        private void UpdateTabHighlights()
        {
            if (_categoryTabs == null) return;
            var active = InventoryService.Instance.ActiveCategory;
            for (int i = 0; i < _categoryTabs.Length; i++)
                _categoryTabs[i].SetSelected((ItemCategory)i == active);
        }

        // ==================== Model 事件回调 ====================

        private void OnModelItemChanged(ItemSO item, int count)
        {
            if (_isOpen)
                RefreshItemGrid();
        }

        private void OnModelItemCountChanged(ItemSO item, int count)
        {
            if (_isOpen)
                RefreshItemGrid();
        }
    }

    /// <summary>
    /// 分类标签按钮 —— 带选中高亮状态。
    /// </summary>
    [System.Serializable]
    public class TabButton
    {
        public Button Button;
        public Image Highlight;
        public TextMeshProUGUI Label;

        public System.Action OnClick;

        public void Initialize()
        {
            if (Button != null)
                Button.onClick.AddListener(() => OnClick?.Invoke());
        }

        public void SetSelected(bool selected)
        {
            if (Highlight != null) Highlight.enabled = selected;
            if (Label != null) Label.color = selected ? Color.white : Color.gray;
        }
    }
}
