using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音量设置页签 —— 管理主音量 / BGM / SFX 三个滑块的显示与交互。
///
/// 挂载到音量页签 GameObject 上，Slider 引用通过 Inspector 赋值。
/// 所有修改实时写入 SettingsManager 并生效。
/// </summary>
public class VolumeSettingsTab : MonoBehaviour
{
    [Header("音量滑块")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("百分比显示（可选）")]
    [SerializeField] private TextMeshProUGUI masterValueText;
    [SerializeField] private TextMeshProUGUI bgmValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    [Header("恢复默认按钮")]
    [SerializeField] private Button resetAudioButton;

    private void OnEnable()
    {
        SyncFromSettings();
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void SyncFromSettings()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        SetSliderWithoutNotify(masterSlider, sm.MasterVolume);
        SetSliderWithoutNotify(bgmSlider, sm.BGMVolume);
        SetSliderWithoutNotify(sfxSlider, sm.SFXVolume);

        UpdateValueText(masterValueText, sm.MasterVolume);
        UpdateValueText(bgmValueText, sm.BGMVolume);
        UpdateValueText(sfxValueText, sm.SFXVolume);
    }

    private void BindEvents()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        if (resetAudioButton != null)
            resetAudioButton.onClick.AddListener(OnResetAudio);
    }

    private void UnbindEvents()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (bgmSlider != null)
            bgmSlider.onValueChanged.RemoveListener(OnBGMChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
        if (resetAudioButton != null)
            resetAudioButton.onClick.RemoveListener(OnResetAudio);
    }

    private void OnMasterChanged(float value)
    {
        SettingsManager.Instance?.SetMasterVolume(value);
        UpdateValueText(masterValueText, value);
    }

    private void OnBGMChanged(float value)
    {
        SettingsManager.Instance?.SetBGMVolume(value);
        UpdateValueText(bgmValueText, value);
    }

    private void OnSFXChanged(float value)
    {
        SettingsManager.Instance?.SetSFXVolume(value);
        UpdateValueText(sfxValueText, value);
    }

    private void OnResetAudio()
    {
        SettingsManager.Instance?.ResetAudioToDefault();
        SyncFromSettings();
    }

    private void SetSliderWithoutNotify(Slider slider, float value)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
    }

    private void UpdateValueText(TextMeshProUGUI text, float value)
    {
        if (text == null) return;
        text.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
