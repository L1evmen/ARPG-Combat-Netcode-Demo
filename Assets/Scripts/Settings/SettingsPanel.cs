using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板容器 —— 管理面板展示/隐藏、页签切换。
/// 由 PausePanel（游戏中）或主菜单按钮调用 Show/Hide。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("根 UI GameObject")]
    [SerializeField] private GameObject uiRoot;

    [Header("页签按钮")]
    [SerializeField] private Button tabVolume;
    [SerializeField] private Button tabKeys;
    [SerializeField] private Button tabDisplay;

    [Header("页签面板")]
    [SerializeField] private GameObject volumeTabGo;
    [SerializeField] private GameObject keysTabGo;
    [SerializeField] private GameObject displayTabGo;

    [Header("子组件引用")]
    [SerializeField] private VolumeSettingsTab volumeTab;
    [SerializeField] private KeyBindingSettingsTab keysTab;

    [Header("关闭按钮（可选）")]
    [SerializeField] private Button closeButton;

    public bool IsVisible => uiRoot != null && uiRoot.activeSelf;

    private enum Tab { Volume, Keys, Display }
    private Tab _currentTab = Tab.Volume;

    private void Start()
    {
        BindTabButtons();

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (uiRoot != null)
            uiRoot.SetActive(true);

        SwitchTab(Tab.Volume);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (keysTab != null)
            keysTab.OnPanelClosing();

        if (uiRoot != null)
            uiRoot.SetActive(false);
        gameObject.SetActive(false);
    }

    // ==================== 页签切换 ====================

    private void BindTabButtons()
    {
        if (tabVolume != null)
            tabVolume.onClick.AddListener(() => SwitchTab(Tab.Volume));
        if (tabKeys != null)
            tabKeys.onClick.AddListener(() => SwitchTab(Tab.Keys));
        if (tabDisplay != null)
            tabDisplay.onClick.AddListener(() => SwitchTab(Tab.Display));
    }

    private void SwitchTab(Tab tab)
    {
        _currentTab = tab;

        if (volumeTabGo != null)
            volumeTabGo.SetActive(tab == Tab.Volume);
        if (keysTabGo != null)
            keysTabGo.SetActive(tab == Tab.Keys);
        if (displayTabGo != null)
            displayTabGo.SetActive(tab == Tab.Display);

        UpdateTabButtonStates();
    }

    private void UpdateTabButtonStates()
    {
        SetTabColor(tabVolume, _currentTab == Tab.Volume);
        SetTabColor(tabKeys, _currentTab == Tab.Keys);
        SetTabColor(tabDisplay, _currentTab == Tab.Display);
    }

    private void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = active ? Color.white : Color.gray;
        btn.colors = colors;
    }
}
