using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerPropertyUI : MonoBehaviour
{
    public static PlayerPropertyUI Instance { get; private set; }
    public TMP_FontAsset uiFont;
    public GameTaskSO activeTask;

    // === 顶部左侧：等级 + 武器槽 ===
    [Header("顶部左侧")]
    public float topLeftOffsetX = 20f;
    public float topLeftOffsetY = -20f;
    public float topLeftGroupHeight = 56f;
    public int topLeftSpacing = 12;
    public int topLeftPaddingLeft = 12;
    public int topLeftPaddingRight = 12;
    public float topLeftExtraWidth = 40f;

    // === 等级 ===
    [Header("等级")]
    public Vector2 levelBoxSize = new Vector2(56, 56);
    public int levelFontSize = 20;

    // === 武器槽 ===
    [Header("武器槽")]
    public float slotsGroupWidth = 360f;
    public int slotsSpacing = 16;
    public int slotsFontSize = 18;
    public int slotsPaddingLeft = 8;

    // === 顶部正中：HP + 精神 ===
    [Header("顶部正中")]
    public float centerPanelOffsetY = -20f;
    public int centerPaddingLeft = 14;
    public int centerPaddingRight = 14;
    public int centerPaddingTop = 8;
    public int centerPaddingBottom = 8;
    public int centerSpacing = 4;

    // === HP 血条 ===
    [Header("HP 血条")]
    public float hpGroupWidth = 240f;
    public float hpBarHeight = 20f;
    public int hpValueFontSize = 13;

    // === 精神条 ===
    [Header("精神条")]
    public float mentalGroupWidth = 180f;
    public float mentalBarHeight = 10f;
    public int mentalValueFontSize = 12;

    // === 右上任务面板 ===
    [Header("任务面板")]
    public float taskPanelWidth = 320f;
    public float taskPanelHeight = 140f;
    public float taskPanelOffsetX = -20f;
    public float taskPanelOffsetY = -20f;
    public int taskTitleFontSize = 20;
    public int taskDescFontSize = 15;
    public int taskProgressFontSize = 18;
    public int taskPaddingLeft = 16;
    public int taskPaddingRight = 16;
    public int taskPaddingTop = 6;
    public int taskPaddingBottom = 12;
    public int taskRowSpacing = 6;

    // === 血条动画 ===
    [Header("血条动画")]
    public float hpLerpSpeed = 6f;

    private Image hpFill;
    private Image mentalFill;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI mentalText;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[4];

    private TextMeshProUGUI taskTitleText;
    private TextMeshProUGUI taskDescText;
    private TextMeshProUGUI taskProgressText;
    private GameObject taskPanel;

    private PlayerProperty pp;
    private float targetHpRatio = 1f;
    private Animator playerAnimator;
    private static readonly int ParamIsUiOpen = Animator.StringToHash("IsUiOpen");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag(Tag.PLAYER);
        pp = player.GetComponent<PlayerProperty>();
        playerAnimator = player.GetComponentInParent<Animator>();
        if (WeaponManager.Instance != null)
            WeaponManager.Instance.OnChanged += UpdateUI;
        UpdateUI();
    }

    void Update()
    {
        if (hpFill != null)
        {
            float current = hpFill.rectTransform.localScale.x;
            if (Mathf.Abs(current - targetHpRatio) > 0.001f)
            {
                float s = Mathf.Lerp(current, targetHpRatio, Time.deltaTime * hpLerpSpeed);
                hpFill.rectTransform.localScale = new Vector3(s, 1, 1);
            }
        }

        bool uiOpen = (GameInventory.InventoryController.Instance != null && GameInventory.InventoryController.Instance.IsOpen)
                   || (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible);
        if (playerAnimator != null)
            playerAnimator.SetBool(ParamIsUiOpen, uiOpen);

        UpdateTaskPanel();
    }

    void OnDestroy()
    {
        if (WeaponManager.Instance != null)
            WeaponManager.Instance.OnChanged -= UpdateUI;
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        BuildTopLeft();
        BuildTopCenter();
        BuildTaskPanel();

        gameObject.name = "HUD";
    }

    // ==================== 顶部左侧：武器槽 ====================
    void BuildTopLeft()
    {
        var panel = MakeRect("TopLeftPanel", transform);
        panel.anchorMin = new Vector2(0, 1);
        panel.anchorMax = new Vector2(0, 1);
        panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = new Vector2(topLeftOffsetX, topLeftOffsetY);
        float totalWidth = slotsGroupWidth + topLeftPaddingLeft + topLeftPaddingRight + topLeftExtraWidth;
        panel.sizeDelta = new Vector2(totalWidth, topLeftGroupHeight);

        var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(topLeftPaddingLeft, topLeftPaddingRight, 0, 0);
        layout.spacing = topLeftSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;

        // 武器槽
        var slotsGroup = MakeRect("SlotsGroup", panel);
        slotsGroup.sizeDelta = new Vector2(slotsGroupWidth, topLeftGroupHeight);
        var slotsLayout = slotsGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
        slotsLayout.padding = new RectOffset(slotsPaddingLeft, 0, 0, 0);
        slotsLayout.spacing = slotsSpacing;
        slotsLayout.childAlignment = TextAnchor.MiddleCenter;
        slotsLayout.childControlWidth = false;
        slotsLayout.childControlHeight = true;

        slotTexts[0] = CreateSlot(slotsGroup, "Slot1", "1 空");
        slotTexts[1] = CreateSlot(slotsGroup, "Slot2", "2 空");
        slotTexts[2] = CreateSlot(slotsGroup, "Slot3", "3 空");
        slotTexts[3] = CreateSlot(slotsGroup, "Slot4", "4 空");
    }

    // ==================== 顶部正中：等级在左 + HP/精神在右 ====================
    void BuildTopCenter()
    {
        float barMaxWidth = Mathf.Max(hpGroupWidth, mentalGroupWidth);
        float barsHeight = centerSpacing + hpBarHeight + centerSpacing + mentalBarHeight;
        float panelHeight = centerPaddingTop + centerPaddingBottom + Mathf.Max(levelBoxSize.y, barsHeight);
        float panelWidth = centerPaddingLeft + centerPaddingRight + levelBoxSize.x + centerSpacing + barMaxWidth;

        var panel = MakeRect("TopCenterPanel", transform);
        panel.anchorMin = new Vector2(0.5f, 1);
        panel.anchorMax = new Vector2(0.5f, 1);
        panel.pivot = new Vector2(0.5f, 1);
        panel.anchoredPosition = new Vector2(0, centerPanelOffsetY);
        panel.sizeDelta = new Vector2(panelWidth, panelHeight);

        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.75f);

        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(centerPaddingLeft, centerPaddingRight, centerPaddingTop, centerPaddingBottom);
        hlg.spacing = centerSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandHeight = true;

        // 左侧：等级
        var lvGroup = MakeRect("LvGroup", panel);
        lvGroup.sizeDelta = new Vector2(levelBoxSize.x, panelHeight - centerPaddingTop - centerPaddingBottom);
        var lvLayout = lvGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
        lvLayout.childAlignment = TextAnchor.MiddleCenter;
        lvLayout.childControlWidth = true;
        lvLayout.childControlHeight = true;
        levelText = MakeText("Level", lvGroup, "Lv.1", levelFontSize, TextAlignmentOptions.Center);
        levelText.color = new Color(1f, 0.85f, 0.3f);

        // 右侧：HP + 精神（上下排列）
        var barsGroup = MakeRect("BarsGroup", panel);
        barsGroup.sizeDelta = new Vector2(barMaxWidth, barsHeight);
        var barsVlg = barsGroup.gameObject.AddComponent<VerticalLayoutGroup>();
        barsVlg.spacing = centerSpacing;
        barsVlg.childAlignment = TextAnchor.MiddleCenter;
        barsVlg.childControlWidth = true;
        barsVlg.childControlHeight = false;
        barsVlg.childForceExpandWidth = true;

        BuildBarRow(barsGroup, "HPRow", barMaxWidth, hpBarHeight, hpValueFontSize,
            new Color(0.85f, 0.25f, 0.25f, 1f), out hpFill, out hpText);

        BuildBarRow(barsGroup, "MentalRow", barMaxWidth, mentalBarHeight, mentalValueFontSize,
            new Color(0.25f, 0.55f, 0.9f, 1f), out mentalFill, out mentalText);
    }

    void BuildBarRow(Transform parent, string name, float totalWidth, float barHeight,
        int valueFontSize, Color fillColor,
        out Image fillImg, out TextMeshProUGUI valueText)
    {
        var row = MakeRect(name, parent);
        row.sizeDelta = new Vector2(totalWidth, barHeight);

        var bgRect = MakeRect(name + "Bg", row);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = Vector2.zero;
        var bgImg = bgRect.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        var fillRect = MakeRect(name + "Fill", bgRect);
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(totalWidth, 0);
        fillImg = fillRect.gameObject.AddComponent<Image>();
        fillImg.color = fillColor;

        valueText = MakeText(name + "Text", bgRect, "100/100", valueFontSize, TextAlignmentOptions.Center);
        valueText.color = Color.white;
        valueText.raycastTarget = false;
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.sizeDelta = Vector2.zero;
    }

    // ==================== 右上任务面板 ====================
    void BuildTaskPanel()
    {
        taskPanel = MakeRect("TaskPanel", transform).gameObject;
        var panelRt = taskPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1, 1);
        panelRt.anchorMax = new Vector2(1, 1);
        panelRt.pivot = new Vector2(1, 1);
        panelRt.anchoredPosition = new Vector2(taskPanelOffsetX, taskPanelOffsetY);
        panelRt.sizeDelta = new Vector2(taskPanelWidth, taskPanelHeight);

        var panelBg = taskPanel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.05f, 0.75f);

        var vlg = taskPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(taskPaddingLeft, taskPaddingRight, taskPaddingTop, taskPaddingBottom);
        vlg.spacing = taskRowSpacing;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;

        taskTitleText = MakeText("TaskTitle", panelRt, "当前无任务", taskTitleFontSize, TextAlignmentOptions.Left);
        taskTitleText.color = new Color(1f, 0.85f, 0.3f);

        taskDescText = MakeText("TaskDesc", panelRt, "", taskDescFontSize, TextAlignmentOptions.Left);
        taskDescText.color = new Color(0.85f, 0.85f, 0.85f);

        taskProgressText = MakeText("TaskProgress", panelRt, "", taskProgressFontSize, TextAlignmentOptions.Left);
        taskProgressText.color = new Color(0.6f, 0.9f, 0.6f);
    }

    void UpdateTaskPanel()
    {
        if (taskPanel == null) return;

        bool hasTask = activeTask != null && activeTask.state == GameTaskState.Executing;

        taskPanel.SetActive(true);

        if (!hasTask)
        {
            if (activeTask != null && activeTask.state == GameTaskState.Completed)
            {
                taskTitleText.text = string.IsNullOrEmpty(activeTask.taskName) ? activeTask.name : activeTask.taskName;
                taskDescText.text = "任务已完成，请返回交任务";
                taskProgressText.text = "";
            }
            else
            {
                taskTitleText.text = "当前无任务";
                taskDescText.text = "";
                taskProgressText.text = "";
            }
            return;
        }

        taskTitleText.text = string.IsNullOrEmpty(activeTask.taskName) ? activeTask.name : activeTask.taskName;
        taskDescText.text = activeTask.taskDescription;
        taskProgressText.text = activeTask.currentEnemyCount + " / " + activeTask.enemyCountNeed;
    }

    RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        return go.AddComponent<RectTransform>();
    }

    TextMeshProUGUI MakeText(string name, Transform parent, string content, int size, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var rt = go.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public void UpdateUI()
    {
        if (pp == null) return;

        targetHpRatio = Mathf.Clamp01(pp.hpValue / 100f);

        if (hpText != null)
            hpText.text = pp.hpValue + "/100";
        if (levelText != null)
            levelText.text = "Lv." + pp.level;

        if (mentalFill != null)
            mentalFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(pp.mentalValue / 100f), 1, 1);
        if (mentalText != null)
            mentalText.text = pp.mentalValue + "/100";

        UpdateSlots();
    }

    TextMeshProUGUI CreateSlot(Transform parent, string name, string label)
    {
        var slotGo = new GameObject(name);
        slotGo.transform.SetParent(parent);
        var rt = slotGo.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;

        var bg = slotGo.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var t = MakeText(name + "Text", slotGo.transform, label, slotsFontSize, TextAlignmentOptions.Center);
        t.color = new Color(0.5f, 0.5f, 0.5f);
        t.raycastTarget = false;
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.sizeDelta = Vector2.zero;
        return t;
    }

    void UpdateSlots()
    {
        var mgr = WeaponManager.Instance;
        var qsm = GameInventory.QuickSlotManager.Instance;

        // 武器槽 0-2
        for (int i = 0; i < 3 && i < slotTexts.Length; i++)
        {
            string txt = (i + 1) + " ";
            txt += mgr != null && mgr.slots[i] != null ? mgr.slots[i].name : "空";
            slotTexts[i].text = txt;
            slotTexts[i].color = mgr != null && mgr.currentIndex == i ? Color.yellow : new Color(0.6f, 0.6f, 0.6f);
        }

        // 快捷栏槽位 3（消耗品）
        if (slotTexts.Length > 3)
        {
            var item = qsm != null ? qsm.GetSlotItem(3) : null;
            string txt = "4 ";
            txt += item != null ? item.name : "空";
            slotTexts[3].text = txt;
        }
    }

    public void UpdatePlayerPropertyUI() => UpdateUI();
}
