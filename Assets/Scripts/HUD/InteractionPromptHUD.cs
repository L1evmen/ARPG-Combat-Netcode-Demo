using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameHUD
{
    /// <summary>
    /// 交互提示 HUD —— 靠近可交互物体/NPC 时出现。
    ///
    /// 显示按键图标 + 简要文字，随距离自动淡入淡出。
    /// </summary>
    public class InteractionPromptHUD : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _keyIcon;
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private float _fadeSpeed = 8f;

        private bool _visible;
        private float _targetAlpha;

        /// <summary>显示交互提示</summary>
        public void Show(Sprite keySprite, string text)
        {
            if (_keyIcon != null) _keyIcon.sprite = keySprite;
            if (_promptText != null) _promptText.text = text;
            _visible = true;
            _targetAlpha = 1f;
        }

        /// <summary>隐藏交互提示</summary>
        public void Hide()
        {
            _visible = false;
            _targetAlpha = 0f;
        }

        private void Update()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
        }
    }
}
