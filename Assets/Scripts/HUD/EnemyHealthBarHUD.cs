using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameHUD
{
    /// <summary>
    /// 敌人血条 HUD —— 锁定敌人时显示。
    ///
    /// 普通敌人：血条锚定在敌人头顶（世界空间 Canvas）。
    /// Boss：屏幕顶部居中显示大型血条 + 名字 + 架势/霸体提示。
    ///
    /// 布局：
    ///   Boss 血条（16:9，锚定顶部居中，偏移 0, -20）：
    ///   ┌──────────────────────────────┐
    ///   │  Boss 名称                  │
    ///   │  ████████████████░░░░ 75%   │
    ///   │  [架势/霸体状态图标]        │
    ///   └──────────────────────────────┘
    /// </summary>
    public class EnemyHealthBarHUD : MonoBehaviour
    {
        [Header("Boss 血条")]
        [SerializeField] private GameObject _bossBarRoot;
        [SerializeField] private TextMeshProUGUI _bossNameText;
        [SerializeField] private Image _bossHpFill;
        [SerializeField] private TextMeshProUGUI _bossHpPercent;
        [SerializeField] private Image _bossStanceIcon;

        [Header("普通锁定血条（世界空间）")]
        [SerializeField] private Canvas _targetBarCanvas;
        [SerializeField] private Image _targetHpFill;
        [SerializeField] private TextMeshProUGUI _targetNameText;

        [Header("平滑过渡")]
        [SerializeField] private float _lerpSpeed = 3f;
        private float _displayBossHp = 1f;
        private float _targetBossHp = 1f;
        private float _displayTargetHp = 1f;
        private float _targetTargetHp = 1f;

        private Transform _lockedTarget;

        private void Start()
        {
            HideBossBar();
            HideTargetBar();
        }

        private void Update()
        {
            // Boss 血条平滑
            _displayBossHp = Mathf.Lerp(_displayBossHp, _targetBossHp, _lerpSpeed * Time.deltaTime);
            if (_bossHpFill != null) _bossHpFill.fillAmount = _displayBossHp;

            // 普通锁定血条跟随目标
            if (_lockedTarget != null)
            {
                _displayTargetHp = Mathf.Lerp(_displayTargetHp, _targetTargetHp, _lerpSpeed * Time.deltaTime);
                if (_targetHpFill != null) _targetHpFill.fillAmount = _displayTargetHp;

                if (Camera.main != null)
                    _targetBarCanvas.transform.position =
                        Camera.main.WorldToScreenPoint(_lockedTarget.position + Vector3.up * 2.5f);
            }
        }

        // ==================== Boss ====================

        public void ShowBossBar(string name, float hpRatio)
        {
            if (_bossBarRoot != null) _bossBarRoot.SetActive(true);
            if (_bossNameText != null) _bossNameText.text = name;
            _targetBossHp = hpRatio;
            _displayBossHp = hpRatio;
            UpdateBossHpText();
        }

        public void UpdateBossHP(float hpRatio)
        {
            _targetBossHp = hpRatio;
            UpdateBossHpText();
        }

        public void HideBossBar()
        {
            if (_bossBarRoot != null) _bossBarRoot.SetActive(false);
        }

        private void UpdateBossHpText()
        {
            if (_bossHpPercent != null)
                _bossHpPercent.text = $"{Mathf.RoundToInt(_targetBossHp * 100)}%";
        }

        // ==================== 普通锁定 ====================

        public void ShowTargetBar(Transform target, string name, float hpRatio)
        {
            _lockedTarget = target;
            if (_targetBarCanvas != null) _targetBarCanvas.gameObject.SetActive(true);
            if (_targetNameText != null) _targetNameText.text = name;
            _targetTargetHp = hpRatio;
            _displayTargetHp = hpRatio;
        }

        public void UpdateTargetHP(float hpRatio)
        {
            _targetTargetHp = hpRatio;
        }

        public void HideTargetBar()
        {
            _lockedTarget = null;
            if (_targetBarCanvas != null) _targetBarCanvas.gameObject.SetActive(false);
        }
    }
}
