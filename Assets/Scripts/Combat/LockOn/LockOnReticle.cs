using UnityEngine;
using UnityEngine.UI;

namespace CombatV2.LockOn
{
    /// <summary>
    /// 锁定标识 —— 在锁定目标身上显示一个图标。
    ///
    /// 配置步骤
    /// 1. 在 Canvas 下创建 Image，挂此脚本
    /// 2. 设置 Reticle Sprite（你的图标）
    /// 3. 调整 World Offset（世界空间偏移，如 Y=2 表示头顶上方 2 米）
    /// 4. Target Lock Manager 拖入或留空自动查找
    /// </summary>
    public class LockOnReticle : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private TargetLockManager _lockManager;
        [SerializeField] private Image _reticleImage;
        [SerializeField] private RectTransform _canvasRect;

        [Header("图标")]
        [Tooltip("拖入你的锁定标识图片")]
        [SerializeField] private Sprite _reticleSprite;

        [Header("位置偏移")]
        [Tooltip("世界空间偏移（相对于 ILockable.LockPoint）")]
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 0.3f, 0f);

        [Tooltip("屏幕空间额外偏移（像素）")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(0f, -30f);

        [Header("动画")]
        [SerializeField] private float _fadeSpeed = 10f;
        [SerializeField] private float _minScale = 0.7f;
        [SerializeField] private float _maxScale = 1.3f;

        private Camera _cam;
        private Transform _target;
        private ILockable _targetLockable;
        private float _currentAlpha;

        private void Awake()
        {
            _cam = Camera.main;

            if (_reticleImage == null)
                _reticleImage = GetComponent<Image>();

            if (_reticleImage != null)
            {
                if (_reticleSprite != null)
                    _reticleImage.sprite = _reticleSprite;

                // 初始透明但不 SetActive(false)，否则会阻止 Start 执行
                Color c = _reticleImage.color;
                c.a = 0f;
                _reticleImage.color = c;
            }
        }

        private void Start()
        {
            if (_lockManager == null)
                _lockManager = FindObjectOfType<TargetLockManager>();

            if (_lockManager != null)
            {
                _lockManager.OnTargetLocked += Show;
                _lockManager.OnTargetUnlocked += Hide;
                Debug.Log($"[LockOnReticle] 已绑定 TargetLockManager 事件 | manager={_lockManager.name}");
            }
            else
            {
                Debug.LogError("[LockOnReticle] 未找到 TargetLockManager！请手动拖入。");
            }
        }

        private void OnValidate()
        {
            if (_reticleImage != null && _reticleSprite != null)
                _reticleImage.sprite = _reticleSprite;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 worldPos = _targetLockable != null
                ? _targetLockable.LockPoint + _worldOffset
                : _target.position + Vector3.up * 1.5f + _worldOffset;

            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f)
            {
                SetAlpha(0f);
                return;
            }

            Vector2 finalScreenPos = (Vector2)screenPos + _screenOffset;

            if (_canvasRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, finalScreenPos, null, out Vector2 anchoredPos);
                (_reticleImage.transform as RectTransform).anchoredPosition = anchoredPos;
            }
            else
            {
                _reticleImage.transform.position = finalScreenPos;
            }

            float dist = Vector3.Distance(_cam.transform.position, worldPos);
            _reticleImage.transform.localScale = Vector3.one * Mathf.Clamp(8f / dist, _minScale, _maxScale);

            SetAlpha(1f);
        }

        public void Show(Transform target)
        {
            Debug.Log($"[LockOnReticle] Show | target={target?.name} | imageActive={_reticleImage != null}");
            _target = target;
            _targetLockable = target != null ? target.GetComponent<ILockable>() : null;
            if (_reticleImage != null)
                _reticleImage.gameObject.SetActive(true);
        }

        public void Hide()
        {
            Debug.Log("[LockOnReticle] Hide");
            _target = null;
            _targetLockable = null;
            if (_reticleImage != null)
                _reticleImage.gameObject.SetActive(false);
            _currentAlpha = 0f;
        }

        private void SetAlpha(float targetAlpha)
        {
            _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * _fadeSpeed);
            if (_reticleImage != null)
            {
                Color c = _reticleImage.color;
                c.a = _currentAlpha;
                _reticleImage.color = c;
            }
        }

        private void OnDestroy()
        {
            if (_lockManager != null)
            {
                _lockManager.OnTargetLocked -= Show;
                _lockManager.OnTargetUnlocked -= Hide;
            }
        }
    }
}
