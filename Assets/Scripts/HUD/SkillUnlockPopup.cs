using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameHUD
{
    /// <summary>
    /// 技能解锁弹窗 —— 从屏幕外滑入中央，文字淡入，停留后滑出。
    ///
    /// Hierarchy 例子：
    ///   SkillUnlockPopup (CanvasGroup)
    ///     └── Panel (Image, 你找的素材)
    ///           └── SkillNameText (TextMeshProUGUI)
    ///
    /// 在 Inspector 中拖入各引用即可。
    /// </summary>
    public class SkillUnlockPopup : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _panel;           // 滑动的面板（放素材图）
        [SerializeField] private Image _icon;                    // 技能图标
        [SerializeField] private TextMeshProUGUI _skillNameText; // 技能名称文字
        [SerializeField] private CanvasGroup _canvasGroup;       // 整体透明度控制

        [Header("动画参数")]
        [SerializeField] private float _slideDistance = 2000f;    // 屏幕外偏移量（像素）
        [SerializeField] private float _slideInDuration = 0.5f;  // 滑入时长
        [SerializeField] private float _textFadeDuration = 0.4f; // 文字淡入时长
        [SerializeField] private float _holdDuration = 2f;       // 停留时长
        [SerializeField] private float _slideOutDuration = 0.4f; // 滑出时长

        private Vector2 _centerPos;
        private Coroutine _running;

        private void Awake()
        {
            // 记录面板在屏幕中央的位置
            _centerPos = _panel.anchoredPosition;
            // 初始隐藏
            _panel.gameObject.SetActive(false);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            CombatEvents.OnSkillUnlocked += OnSkillUnlocked;
        }

        private void OnDisable()
        {
            CombatEvents.OnSkillUnlocked -= OnSkillUnlocked;
        }

        private void OnSkillUnlocked(string skillId)
        {
            // 获取显示名和图标
            string displayName = skillId;
            Sprite icon = null;
            var hud = FindObjectOfType<SkillBarHUD>();
            if (hud != null)
            {
                string name = hud.GetSlotDisplayName(skillId);
                if (!string.IsNullOrEmpty(name))
                    displayName = name;
                icon = hud.GetSlotIcon(skillId);
            }

            Show(displayName, icon);
        }

        /// <summary>外部也可调用：弹出指定文字的解锁弹窗</summary>
        public void Show(string skillName, Sprite icon = null)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PlaySequence(skillName, icon));
        }

        private IEnumerator PlaySequence(string skillName, Sprite icon)
        {
            _panel.gameObject.SetActive(true);

            // 设置图标
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            // 设置文字（先隐藏）
            if (_skillNameText != null)
            {
                _skillNameText.text = skillName;
                _skillNameText.alpha = 0f;
            }

            // === 1. 从屏幕右侧滑入中央 ===
            Vector2 startPos = _centerPos + Vector2.right * _slideDistance;
            _panel.anchoredPosition = startPos;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            yield return AnimatePosition(_panel, startPos, _centerPos, _slideInDuration);

            // === 2. 文字淡入 ===
            if (_skillNameText != null)
                yield return AnimateAlpha(_skillNameText, 0f, 1f, _textFadeDuration);

            // === 3. 停留 ===
            yield return new WaitForSeconds(_holdDuration);

            // === 4. 滑出屏幕左侧 ===
            Vector2 endPos = _centerPos + Vector2.left * _slideDistance;
            // 文字和面板一起滑出
            yield return AnimatePosition(_panel, _centerPos, endPos, _slideOutDuration);

            // 收尾
            _panel.gameObject.SetActive(false);
            _panel.anchoredPosition = _centerPos;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            _running = null;
        }

        // ==================== 动画工具 ====================

        private IEnumerator AnimatePosition(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                yield return null;
            }
            rt.anchoredPosition = to;
        }

        private IEnumerator AnimateAlpha(TextMeshProUGUI tmp, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tmp.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            tmp.alpha = to;
        }

        /// <summary>缓出曲线：快→慢</summary>
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    }
}
