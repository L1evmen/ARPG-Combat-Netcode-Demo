using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 血条 —— 屏幕顶部居中，显示 HP / 格挡耐力。
///
/// 可复用于任意 Boss：Show(bossName) 打开，Hide() 关闭，
/// UpdateHP / UpdateBlockStamina 每帧驱动。
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("根节点")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Boss 名称")]
    [SerializeField] private TextMeshProUGUI _bossNameText;

    [Header("HP 血条")]
    [SerializeField] private Image _hpBg;
    [SerializeField] private Image _hpFill;
    [SerializeField] private TextMeshProUGUI _hpPercentText;
    [Range(0.5f, 10f)]
    [SerializeField] private float _hpLerpSpeed = 4f;

    [Header("格挡耐力条（可选）")]
    [SerializeField] private Image _blockBg;
    [SerializeField] private Image _blockFill;
    [Range(0.5f, 10f)]
    [SerializeField] private float _blockLerpSpeed = 3f;

    private float _targetHP = 1f;
    private float _displayHP = 1f;
    private float _targetBlock = 1f;
    private float _displayBlock = 1f;

    private bool _isVisible;

    public void Show(string bossName)
    {
        if (_isVisible) return;

        _isVisible = true;
        if (_panelRoot != null) _panelRoot.SetActive(true);

        if (_bossNameText != null)
            _bossNameText.text = bossName;

        _targetHP = 1f;
        _displayHP = 1f;
        _targetBlock = 1f;
        _displayBlock = 1f;

        // 确保耐力条常驻可见（修复旧逻辑隐藏后无法恢复）
        if (_blockBg != null) _blockBg.gameObject.SetActive(true);
        if (_blockFill != null) _blockFill.gameObject.SetActive(true);

        ApplyHP();
        ApplyBlock();
    }

    public void Hide()
    {
        _isVisible = false;
        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    public void UpdateHP(float ratio)
    {
        _targetHP = Mathf.Clamp01(ratio);
        if (_hpPercentText != null)
            _hpPercentText.text = $"{Mathf.RoundToInt(_targetHP * 100)}%";
    }

    public void UpdateBlockStamina(float ratio)
    {
        _targetBlock = Mathf.Clamp01(ratio);
    }

    private void Update()
    {
        if (!_isVisible) return;

        _displayHP = Mathf.Lerp(_displayHP, _targetHP, _hpLerpSpeed * Time.deltaTime);
        if (Mathf.Abs(_displayHP - _targetHP) < 0.001f) _displayHP = _targetHP;
        ApplyHP();

        _displayBlock = Mathf.Lerp(_displayBlock, _targetBlock, _blockLerpSpeed * Time.deltaTime);
        if (Mathf.Abs(_displayBlock - _targetBlock) < 0.001f) _displayBlock = _targetBlock;
        ApplyBlock();
    }

    private void ApplyHP()
    {
        if (_hpFill != null) _hpFill.fillAmount = _displayHP;
    }

    private void ApplyBlock()
    {
        if (_blockFill == null) return;
        _blockFill.fillAmount = _displayBlock;
    }
}
