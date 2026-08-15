using System;
using UnityEngine;

/// <summary>
/// 游戏全局设置 —— ScriptableObject 配置容器。
///
/// 存储所有用户可配置的运行时设置：
///   - 音频音量（主 / BGM / SFX）
///   - 键位绑定（逻辑动作 → 物理按键）
///   - 鼠标灵敏度
///   - 后续可扩展：语言、画质、屏幕分辨率等
///
/// 使用方式：
///   1. 在 Editor 中右键 → Create → Game/Settings 创建资产。
///   2. 放入 Resources/ 目录，SettingsManager 在启动时自动加载。
///   3. 运行时修改通过 SettingsManager.Apply() 提交，保存到 PlayerPrefs。
/// </summary>
[CreateAssetMenu(fileName = "GameSettings", menuName = "Game/Settings", order = 100)]
public class GameSettings : ScriptableObject
{
    // ==================== 音频设置 ====================

    [Header("音频")]
    [Range(0f, 1f)]
    [SerializeField] private float _masterVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float _bgmVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 1f;

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Mathf.Clamp01(value);
    }
    public float BGMVolume
    {
        get => _bgmVolume;
        set => _bgmVolume = Mathf.Clamp01(value);
    }
    public float SFXVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Mathf.Clamp01(value);
    }

    // ==================== 输入设置 ====================

    [Header("输入")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _mouseSensitivity = 1f;

    public float MouseSensitivity
    {
        get => _mouseSensitivity;
        set => _mouseSensitivity = Mathf.Clamp(value, 0.1f, 5f);
    }

    /// <summary>键位绑定列表（逻辑动作名 → Input System Binding Path）</summary>
    [SerializeField]
    private KeyBinding[] _keyBindings = new KeyBinding[]
    {
        new KeyBinding { ActionName = "Attack",   DisplayName = "攻击",   BindingPath = "<Mouse>/leftButton" },
        new KeyBinding { ActionName = "Jump",     DisplayName = "跳跃",   BindingPath = "<Keyboard>/space" },
        new KeyBinding { ActionName = "Sprint",   DisplayName = "冲刺",   BindingPath = "<Keyboard>/leftShift" },
        new KeyBinding { ActionName = "Crouch",   DisplayName = "蹲下",   BindingPath = "<Keyboard>/c" },
        new KeyBinding { ActionName = "Ctrl",     DisplayName = "切换模式", BindingPath = "<Keyboard>/leftCtrl" },
        new KeyBinding { ActionName = "Interact", DisplayName = "交互",    BindingPath = "<Keyboard>/e" },
    };

    public KeyBinding[] KeyBindings
    {
        get => _keyBindings;
        set => _keyBindings = value;
    }

    /// <summary>按动作名查找绑定路径，未找到返回空字符串</summary>
    public string GetBindingPath(string actionName)
    {
        if (_keyBindings == null) return string.Empty;
        foreach (var kb in _keyBindings)
            if (kb.ActionName == actionName)
                return kb.BindingPath;
        return string.Empty;
    }

    // ==================== 显示设置（预留） ====================

    [Header("显示（预留）")]
    [SerializeField] private int _targetFrameRate = 60;
    [SerializeField] private bool _vSync = true;

    public int TargetFrameRate
    {
        get => _targetFrameRate;
        set => _targetFrameRate = Mathf.Max(30, value);
    }
    public bool VSync
    {
        get => _vSync;
        set => _vSync = value;
    }

    // ==================== 内部类型 ====================

    /// <summary>
    /// 键位绑定条目 —— 将逻辑动作映射到物理按键路径。
    ///
    /// BindingPath 使用 Unity Input System 的路径格式：
    ///   "<Mouse>/leftButton", "<Keyboard>/w", "<Gamepad>/buttonSouth" 等。
    /// </summary>
    [Serializable]
    public struct KeyBinding
    {
        [Tooltip("逻辑动作名，必须与 InputActionAsset 中的 Action 名一致")]
        public string ActionName;

        [Tooltip("UI 显示名称，如 轻攻击")]
        public string DisplayName;

        [Tooltip("Input System 绑定路径，如 <Mouse>/leftButton")]
        public string BindingPath;
    }
}
