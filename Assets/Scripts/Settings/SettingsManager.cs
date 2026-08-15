using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 设置管理器 —— 全局单例，负责加载 / 保存 / 分发游戏设置。
///
/// 音频设置持久化到 PlayerPrefs，按键绑定持久化到 KeyConfig/keybindings.json。
/// </summary>
public class SettingsManager : MonoBehaviour
{
    // ==================== 单例 ====================

    public static SettingsManager Instance { get; private set; }

    // ==================== JSON 路径 ====================

    private static string KeyBindingsPath =>
        Path.Combine(Application.dataPath, "..", "KeyConfig", "keybindings.json");

    // ==================== 运行时设置数据 ====================

    private GameSettings _settings;
    public GameSettings Settings => _settings;

    /// <summary>所有按键绑定条目（从 JSON 加载或自动生成）</summary>
    private List<KeyBindingEntry> _keyBindings = new List<KeyBindingEntry>();
    public IReadOnlyList<KeyBindingEntry> KeyBindings => _keyBindings;

    // ==================== 按键绑定条目 ====================

    [Serializable]
    public class KeyBindingEntry
    {
        public string action;
        public string displayName;
        public string defaultPath;
        public string currentPath; // 空 = 未修改，使用默认值
    }

    /// <summary>获取动作的当前有效绑定路径</summary>
    public string GetEffectivePath(KeyBindingEntry entry)
    {
        return string.IsNullOrEmpty(entry.currentPath) ? entry.defaultPath : entry.currentPath;
    }

    // ==================== JSON 结构 ====================

    [Serializable]
    private class KeyBindingsData
    {
        public List<KeyBindingEntry> bindings;
    }

    // ==================== 显示名映射（自动生成 JSON 时用） ====================

    private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        { "Jump",     "跳跃" },
        { "Sprint",   "冲刺" },
        { "Ctrl",     "切换模式" },
        { "Crouch",   "蹲下" },
        { "Attack",   "攻击" },
        { "Interact", "交互" },
    };

    // ==================== 事件 ====================

    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnBGMVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    public event Action<float> OnSensitivityChanged;
    public event Action<string, string> OnKeyBindingChanged;
    public event Action OnSettingsApplied;

    // ==================== 便捷属性 ====================

    public float MasterVolume
    {
        get => _settings.MasterVolume;
        set => _settings.MasterVolume = value;
    }

    public float BGMVolume
    {
        get => _settings.BGMVolume;
        set => _settings.BGMVolume = value;
    }

    public float SFXVolume
    {
        get => _settings.SFXVolume;
        set => _settings.SFXVolume = value;
    }

    public float MouseSensitivity
    {
        get => _settings.MouseSensitivity;
        set => _settings.MouseSensitivity = value;
    }

    // ==================== 生命周期 ====================

    private void Awake()
    {
        if (Instance != null)
        {
            // 已有跨场景实例时，仅销毁本组件，保留 SettingsPanel UI 正常工作
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        LoadKeyBindings();
        ApplyDisplaySettings();
    }

    // ==================== 音频加载 ====================

    private void LoadSettings()
    {
        var template = Resources.Load<GameSettings>("GameSettings");
        _settings = template != null ? Instantiate(template) : ScriptableObject.CreateInstance<GameSettings>();

        _settings.MasterVolume = PlayerPrefs.GetFloat("Audio_MasterVolume", _settings.MasterVolume);
        _settings.BGMVolume = PlayerPrefs.GetFloat("Audio_BGMVolume", _settings.BGMVolume);
        _settings.SFXVolume = PlayerPrefs.GetFloat("Audio_SFXVolume", _settings.SFXVolume);
        _settings.MouseSensitivity = PlayerPrefs.GetFloat("Input_MouseSensitivity", _settings.MouseSensitivity);
        _settings.TargetFrameRate = PlayerPrefs.GetInt("Display_TargetFPS", _settings.TargetFrameRate);
        _settings.VSync = PlayerPrefs.GetInt("Display_VSync", _settings.VSync ? 1 : 0) == 1;
    }

    // ==================== 按键绑定加载 ====================

    private void LoadKeyBindings()
    {
        _keyBindings.Clear();

        if (File.Exists(KeyBindingsPath))
        {
            // 从 JSON 加载
            try
            {
                string json = File.ReadAllText(KeyBindingsPath);
                var data = JsonUtility.FromJson<KeyBindingsData>(json);
                if (data?.bindings != null)
                    _keyBindings = data.bindings;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SettingsManager] JSON 解析失败，重新生成。\n{e.Message}");
            }
        }

        // JSON 不存在或解析失败 → 从 InputActionAsset 自动生成
        if (_keyBindings.Count == 0)
        {
            GenerateDefaultBindings();
        }
    }

    /// <summary>从 InputActionAsset 自动发现所有 Button 动作，生成默认 JSON</summary>
    private void GenerateDefaultBindings()
    {
        _keyBindings.Clear();

        // 通过查找场景中的 PlayerInput 获取 actions
        var pi = FindObjectOfType<PlayerInput>();
        if (pi == null)
        {
            Debug.LogWarning("[SettingsManager] 场景中无 PlayerInput，无法自动生成按键绑定");
            return;
        }

        foreach (var action in pi.actions)
        {
            if (action.type != InputActionType.Button) continue;
            if (action.bindings.Count == 0) continue;

            string name = action.name;
            string defaultPath = action.bindings[0].path ?? string.Empty;
            DisplayNames.TryGetValue(name, out string displayName);
            if (string.IsNullOrEmpty(displayName)) displayName = name;

            _keyBindings.Add(new KeyBindingEntry
            {
                action = name,
                displayName = displayName,
                defaultPath = defaultPath,
                currentPath = string.Empty
            });
        }

        SaveKeyBindings();
        Debug.Log($"[SettingsManager] 已自动生成 {_keyBindings.Count} 个按键绑定到 {KeyBindingsPath}");
    }

    /// <summary>保存按键绑定到 JSON 文件</summary>
    public void SaveKeyBindings()
    {
        var data = new KeyBindingsData { bindings = _keyBindings };

        try
        {
            string dir = Path.GetDirectoryName(KeyBindingsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(KeyBindingsPath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SettingsManager] 保存 JSON 失败。\n{e.Message}");
        }
    }

    // ==================== 音频快速设置 ====================

    public void SetMasterVolume(float value)
    {
        MasterVolume = value;
        PlayerPrefs.SetFloat("Audio_MasterVolume", value);
        PlayerPrefs.Save();
        OnMasterVolumeChanged?.Invoke(value);
    }

    public void SetBGMVolume(float value)
    {
        BGMVolume = value;
        PlayerPrefs.SetFloat("Audio_BGMVolume", value);
        PlayerPrefs.Save();
        OnBGMVolumeChanged?.Invoke(value);
    }

    public void SetSFXVolume(float value)
    {
        SFXVolume = value;
        PlayerPrefs.SetFloat("Audio_SFXVolume", value);
        PlayerPrefs.Save();
        OnSFXVolumeChanged?.Invoke(value);
    }

    // ==================== 按键修改 ====================

    /// <summary>修改指定动作的 currentPath 并保存</summary>
    public void SetKeyBinding(string actionName, string newPath)
    {
        var entry = _keyBindings.Find(e => e.action == actionName);
        if (entry == null) return;

        entry.currentPath = newPath;
        SaveKeyBindings();
        OnKeyBindingChanged?.Invoke(actionName, newPath);
    }

    // ==================== 重置 ====================

    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey("Audio_MasterVolume");
        PlayerPrefs.DeleteKey("Audio_BGMVolume");
        PlayerPrefs.DeleteKey("Audio_SFXVolume");
        PlayerPrefs.DeleteKey("Input_MouseSensitivity");
        PlayerPrefs.DeleteKey("Display_TargetFPS");
        PlayerPrefs.DeleteKey("Display_VSync");
        PlayerPrefs.Save();

        ResetKeyBindingsToDefault();
        LoadSettings();
        FireEvents();
        ApplyDisplaySettings();
    }

    public void ResetAudioToDefault()
    {
        PlayerPrefs.DeleteKey("Audio_MasterVolume");
        PlayerPrefs.DeleteKey("Audio_BGMVolume");
        PlayerPrefs.DeleteKey("Audio_SFXVolume");
        PlayerPrefs.Save();

        LoadSettings();
        FireEvents();
    }

    /// <summary>将所有动作的 currentPath 设为空，回到默认值</summary>
    public void ResetKeyBindingsToDefault()
    {
        foreach (var entry in _keyBindings)
            entry.currentPath = string.Empty;

        SaveKeyBindings();
        FireEvents();
    }

    private void FireEvents()
    {
        OnMasterVolumeChanged?.Invoke(_settings.MasterVolume);
        OnBGMVolumeChanged?.Invoke(_settings.BGMVolume);
        OnSFXVolumeChanged?.Invoke(_settings.SFXVolume);
        OnSensitivityChanged?.Invoke(_settings.MouseSensitivity);
        OnSettingsApplied?.Invoke();
    }

    // ==================== 应用到 Input System ====================

    /// <summary>
    /// 将当前绑定应用到 PlayerInput。
    /// currentPath 为空时使用 defaultPath，不为空时使用 currentPath。
    /// </summary>
    public void ApplyAllBindingOverrides(PlayerInput playerInput)
    {
        if (playerInput == null) return;

        // 先清除所有已有覆盖
        foreach (var action in playerInput.actions)
            action.RemoveAllBindingOverrides();

        foreach (var entry in _keyBindings)
        {
            string effectivePath = GetEffectivePath(entry);
            if (string.IsNullOrEmpty(effectivePath)) continue;

            var action = playerInput.actions.FindAction(entry.action);
            if (action == null) continue;

            action.ApplyBindingOverride(new InputBinding { overridePath = effectivePath });
        }
    }

    // ==================== 查询 ====================

    public string GetBindingPathForAction(string actionName)
    {
        var entry = _keyBindings.Find(e => e.action == actionName);
        return entry != null ? GetEffectivePath(entry) : string.Empty;
    }

    // ==================== 其他 ====================

    public void Apply()
    {
        PlayerPrefs.SetFloat("Audio_MasterVolume", _settings.MasterVolume);
        PlayerPrefs.SetFloat("Audio_BGMVolume", _settings.BGMVolume);
        PlayerPrefs.SetFloat("Audio_SFXVolume", _settings.SFXVolume);
        PlayerPrefs.SetFloat("Input_MouseSensitivity", _settings.MouseSensitivity);
        PlayerPrefs.SetInt("Display_TargetFPS", _settings.TargetFrameRate);
        PlayerPrefs.SetInt("Display_VSync", _settings.VSync ? 1 : 0);
        PlayerPrefs.Save();

        FireEvents();
        ApplyDisplaySettings();
    }

    private void ApplyDisplaySettings()
    {
        Application.targetFrameRate = _settings.TargetFrameRate;
        QualitySettings.vSyncCount = _settings.VSync ? 1 : 0;
    }
}
