using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 主菜单管理器 —— 控制开始界面的按钮逻辑。
///
/// 挂载到主菜单场景的 Canvas 上。
/// Inspector 中绑定 4 个主按钮 + 存档槽位面板。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("主面板")]
    [SerializeField] private GameObject mainPanel;

    [Header("主按钮")]
    [SerializeField] private Button btnNewGame;
    [SerializeField] private Button btnContinue;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnQuit;

    [Header("存档槽位面板")]
    [SerializeField] private GameObject slotPanel;
    [SerializeField] private Button[] slotButtons = new Button[SaveManager.MAX_SLOTS];
    [SerializeField] private TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[SaveManager.MAX_SLOTS];
    [SerializeField] private Button btnSlotBack;

    [Header("设置面板")]
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private Button btnSettingsBack;
    private SettingsPanel _settingsPanelInstance;

    private bool _isNewGameMode;

    private void Start()
    {
        btnNewGame.onClick.AddListener(OnNewGame);
        btnContinue.onClick.AddListener(OnContinue);
        btnSettings.onClick.AddListener(OnSettings);
        btnQuit.onClick.AddListener(OnQuit);

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;
            slotButtons[i].onClick.AddListener(() => OnSlotSelected(index));
        }

        if (btnSlotBack != null)
            btnSlotBack.onClick.AddListener(HideSlotPanel);

        if (btnSettingsBack != null)
            btnSettingsBack.onClick.AddListener(HideSettingsPanel);

        slotPanel.SetActive(false);
        _settingsPanelInstance = settingsPanel;
        if (_settingsPanelInstance != null)
            _settingsPanelInstance.Hide();
        UpdateContinueButton();
    }

    // ==================== 主按钮 ====================

    private void OnNewGame()
    {
        // 自动选择第一个空存档槽
        for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
        {
            if (!SaveManager.HasSave(i))
            {
                GameManager.Instance.StartNewGame(i);
                return;
            }
        }
        // 全满，弹出选择让玩家覆盖
        _isNewGameMode = true;
        ShowSlotPanel();
    }

    private void OnContinue()
    {
        _isNewGameMode = false;
        ShowSlotPanel();
    }

    private void OnSettings()
    {
        if (_settingsPanelInstance == null)
            _settingsPanelInstance = FindObjectOfType<SettingsPanel>();
        if (_settingsPanelInstance == null)
        {
            Debug.LogWarning("[MainMenu] 找不到 SettingsPanel");
            return;
        }
        mainPanel.SetActive(false);
        _settingsPanelInstance.Show();
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== 面板切换 ====================

    private void ShowSlotPanel()
    {
        RefreshSlotUI();
        mainPanel.SetActive(false);
        slotPanel.SetActive(true);
    }

    private void HideSlotPanel()
    {
        slotPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    private void HideSettingsPanel()
    {
        if (_settingsPanelInstance == null)
            _settingsPanelInstance = FindObjectOfType<SettingsPanel>();
        _settingsPanelInstance?.Hide();
        mainPanel.SetActive(true);
    }

    // ==================== 存档槽位 ====================

    private void RefreshSlotUI()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var info = SaveManager.GetSaveInfo(i);
            if (slotTexts[i] != null)
            {
                if (info != null)
                    slotTexts[i].text = $"存档 {i + 1}\nLv.{info.level}  {info.saveTime}";
                else
                    slotTexts[i].text = $"存档 {i + 1}\n-- 空 --";
            }

            if (!_isNewGameMode)
                slotButtons[i].interactable = info != null;
            else
                slotButtons[i].interactable = true;
        }
    }

    private void OnSlotSelected(int slotIndex)
    {
        if (_isNewGameMode)
            GameManager.Instance.StartNewGame(slotIndex);
        else
            GameManager.Instance.ContinueGame(slotIndex);
    }

    // ==================== 辅助 ====================

    private void UpdateContinueButton() { }
}
