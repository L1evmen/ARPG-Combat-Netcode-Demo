using UnityEngine;
using UnityEngine.SceneManagement;
using GameInventory;

/// <summary>
/// 游戏管理器 —— 跨场景传递"新游戏/继续游戏"状态。
///
/// 由 MainMenu 场景的按钮设置，GameScene 加载后由 SaveManager 读取。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>true=新游戏，false=继续游戏</summary>
    public bool IsNewGame { get; set; } = true;

    /// <summary>当前选择的存档槽位</summary>
    public int SelectedSlot { get; set; } = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>开始新游戏</summary>
    public void StartNewGame(int slotIndex)
    {
        IsNewGame = true;
        SelectedSlot = slotIndex;
        SaveManager.DeleteSave(slotIndex);
        SceneManager.LoadScene("01-GameScene");
    }

    /// <summary>继续游戏</summary>
    public void ContinueGame(int slotIndex)
    {
        IsNewGame = false;
        SelectedSlot = slotIndex;
        SceneManager.LoadScene("01-GameScene");
    }

    /// <summary>保存并返回主菜单 —— 清理所有 DontDestroyOnLoad 的游戏对象</summary>
    public void ReturnToMainMenu()
    {
        // 销毁所有跨场景的游戏管理器（重新进入主菜单时会重新创建）
        DestroyIfAlive(SaveManager.Instance?.gameObject);
        DestroyIfAlive(InventoryService.Instance?.gameObject);
        DestroyIfAlive(AudioManager.Instance?.gameObject);

        // 销毁自身
        Instance = null;
        Destroy(gameObject);

        SceneManager.LoadScene("00-Mainmeun");
    }

    private static void DestroyIfAlive(GameObject go)
    {
        if (go != null) Destroy(go);
    }
}
