using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameHUD
{
    /// <summary>
    /// 玩家状态栏 HUD —— 左上角常驻显示。
    ///
    /// 包含：血量条（红色）+ 法力条（蓝色）+ 耐力条（黄色）+ 棍势点（圆形指示器，0-3段）。
    /// 蓄力时棍势点逐级亮起；释放后归零。
    ///
    /// 布局参考（16:9，锚定左上角，偏移 40, -40）：
    ///   ┌─────────────────────────────┐
    ///   │ [Lv.XX]                     │
    ///   │ ━━━━━━━━━━ HP (红) 100/100  │
    ///   │ ━━━━━━━━━━ MP (蓝) 80/80   │
    ///   │ ━━━━━━━━━━ SP (黄) 50/50   │
    ///   │ ● ● ● ○ 棍势 (4段)         │
    ///   └─────────────────────────────┘
    /// </summary>
    public class PlayerStatusHUD : MonoBehaviour
    {
        private PlayerProperty _prop;

        private void OnEnable()
        {
            CombatEvents.OnChargeProgress += OnChargeProgress;
            CombatEvents.OnChargeEnded += OnChargeEnded;
        }

        private void OnDisable()
        {
            CombatEvents.OnChargeProgress -= OnChargeProgress;
            CombatEvents.OnChargeEnded -= OnChargeEnded;
        }

        private void OnChargeProgress(int levelIndex, int totalLevels, float progress)
        {
            // ComboManager 的 chargeLevel: -1=未达门槛, 0=L1, 1=L2, 2=L3
            // 映射到棍势点: 0~3 格（level + 1，负数归零）
            ChargePoints = Mathf.Clamp(levelIndex + 1, 0, _chargeDots?.Length ?? 4);
        }

        private void OnChargeEnded()
        {
            ChargePoints = 0;
        }

        [Header("血量")]
        [SerializeField] private Image _hpFill;
        [SerializeField] private TextMeshProUGUI _hpText;
        [Range(0f, 1f)] public float HpRatio = 1f;
        public int HpCurrent = 100;
        public int HpMax = 100;

        [Header("法力")]
        [SerializeField] private Image _mpFill;
        [SerializeField] private TextMeshProUGUI _mpText;
        public int MpCurrent = 80;
        public int MpMax = 80;

        [Header("耐力")]
        [SerializeField] private Image _spFill;
        [SerializeField] private TextMeshProUGUI _spText;
        public int SpCurrent = 50;
        public int SpMax = 50;

        [Header("棍势点")]
        [SerializeField] private Image[] _chargeDots;  // 4个圆形指示器
        public int ChargePoints;

        [Header("平滑过渡")]
        [SerializeField] private float _lerpSpeed = 5f;
        private float _displayHpRatio = 1f;
        private float _displayMpRatio = 1f;
        private float _displaySpRatio = 1f;

        private void Update()
        {
            // 每帧从 PlayerProperty 拉取最新数据（避免初始化时序问题）
            if (_prop == null)
                _prop = FindObjectOfType<PlayerProperty>();
            if (_prop != null)
            {
                HpCurrent = _prop.hpValue;
                HpMax = _prop.hpMax;
                HpRatio = HpMax > 0 ? (float)HpCurrent / HpMax : 0f;
                MpCurrent = _prop.energyValue;
                MpMax = _prop.energyMax;
                SpCurrent = _prop.mentalValue;
                SpMax = _prop.mentalMax;
            }

            float mpRatio = MpMax > 0 ? (float)MpCurrent / MpMax : 0f;
            float spRatio = SpMax > 0 ? (float)SpCurrent / SpMax : 0f;

            // 三个条统一平滑过渡
            _displayHpRatio = Mathf.Lerp(_displayHpRatio, HpRatio, _lerpSpeed * Time.deltaTime);
            _displayMpRatio = Mathf.Lerp(_displayMpRatio, mpRatio, _lerpSpeed * Time.deltaTime);
            _displaySpRatio = Mathf.Lerp(_displaySpRatio, spRatio, _lerpSpeed * Time.deltaTime);

            // 血条
            if (_hpFill != null) _hpFill.fillAmount = _displayHpRatio;
            if (_hpText != null) _hpText.text = $"{Mathf.RoundToInt(HpMax * _displayHpRatio)}/{HpMax}";

            // 法力
            if (_mpFill != null) _mpFill.fillAmount = _displayMpRatio;
            if (_mpText != null) _mpText.text = $"{Mathf.RoundToInt(MpMax * _displayMpRatio)}/{MpMax}";

            // 耐力
            if (_spFill != null) _spFill.fillAmount = _displaySpRatio;
            if (_spText != null) _spText.text = $"{Mathf.RoundToInt(SpMax * _displaySpRatio)}/{SpMax}";

            // 棍势点
            UpdateChargeDots();
        }

        private void UpdateChargeDots()
        {
            if (_chargeDots == null) return;
            for (int i = 0; i < _chargeDots.Length; i++)
            {
                if (_chargeDots[i] == null) continue;
                _chargeDots[i].enabled = i < ChargePoints;
                _chargeDots[i].color = ChargePoints >= _chargeDots.Length
                    ? new Color(1f, 0.8f, 0.2f)  // 满蓄金光
                    : new Color(1f, 1f, 1f);
            }
        }

        // ==================== 公开 API（由 PlayerProperty 或其他系统调用） ====================

        public void SetHP(int current, int max)
        {
            HpCurrent = current;
            HpMax = max;
            HpRatio = max > 0 ? (float)current / max : 0f;
        }

        public void SetMP(int current, int max)
        {
            MpCurrent = current;
            MpMax = max;
        }

        public void SetSP(int current, int max)
        {
            SpCurrent = current;
            SpMax = max;
        }

        public void SetChargePoints(int points)
        {
            ChargePoints = Mathf.Clamp(points, 0, 4);
        }
    }
}
