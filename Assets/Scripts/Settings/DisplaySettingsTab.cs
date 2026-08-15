using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示设置页签 —— 分辨率、全屏。
///
/// 挂载到显示页签 GameObject 上，UI 引用通过 Inspector 赋值。
/// </summary>
public class DisplaySettingsTab : MonoBehaviour
{
    [Header("分辨率")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("全屏")]
    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] _resolutions;

    private void OnEnable()
    {
        InitResolutions();
        SyncFromSettings();
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    // ==================== 初始化 ====================

    private void InitResolutions()
    {
        _resolutions = Screen.resolutions;
        var options = new List<string>();
        int currentIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            options.Add($"{r.width} x {r.height} @{r.refreshRateRatio.value:F0}Hz");

            if (r.width == Screen.currentResolution.width
             && r.height == Screen.currentResolution.height
             && r.refreshRateRatio.value == Screen.currentResolution.refreshRateRatio.value)
                currentIndex = i;
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(currentIndex);
        }
    }

    // ==================== 同步 ====================

    private void SyncFromSettings()
    {
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    // ==================== 事件绑定 ====================

    private void BindEvents()
    {
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void UnbindEvents()
    {
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
    }

    // ==================== 回调 ====================

    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= _resolutions.Length) return;
        var r = _resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
    }

    private void OnFullscreenChanged(bool isOn)
    {
        Screen.fullScreen = isOn;
    }
}
