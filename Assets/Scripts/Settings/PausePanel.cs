using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 暂停面板 —— Esc 唤起，显示"关闭"、"设置"、"保存"和"退出游戏"按钮。
/// 打开时暂停游戏（Time.timeScale = 0），关闭时恢复。
/// </summary>
public class PausePanel : MonoBehaviour
{
    public static PausePanel Instance { get; private set; }

    [Header("根 UI")]
    [SerializeField] private GameObject uiRoot;

    [Header("按钮")]
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnQuit;

    [Header("设置面板引用")]
    [SerializeField] private SettingsPanel settingsPanel;

    public bool IsVisible => uiRoot != null && uiRoot.activeSelf;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (uiRoot != null)
            uiRoot.SetActive(false);

        // 游戏启动时锁定光标（正常游玩状态）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (btnClose != null)
            btnClose.onClick.AddListener(Hide);

        if (btnSettings != null)
            btnSettings.onClick.AddListener(OpenSettings);

        if (btnSave != null)
            btnSave.onClick.AddListener(Save);

        if (btnQuit != null)
            btnQuit.onClick.AddListener(QuitGame);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 优先级1：设置面板打开 → 关设置
        if (settingsPanel != null && settingsPanel.IsVisible)
        {
            settingsPanel.Hide();
            return;
        }

        // 优先级2：暂停面板打开 → 关暂停，恢复游戏
        if (IsVisible)
        {
            Hide();
            return;
        }

        // 优先级3：都没开 → 开暂停
        Show();
    }

    public void Show()
    {
        if (uiRoot != null)
            uiRoot.SetActive(true);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (uiRoot != null)
            uiRoot.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.Show();
    }

    private void Save()
    {
        SaveManager.Instance?.Save();
        Hide();
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.ReturnToMainMenu();
    }
}
