using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;
using TMPro;

namespace GameHUD
{
    /// <summary>
    /// 浮动文字管理器 —— 伤害/治疗/暴击数字弹出。
    ///
    /// 使用 ObjectPool 避免 GC。伤害数字上飘渐隐，暴击字体更大。
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [Header("预制体")]
        [SerializeField] private FloatingText _damagePrefab;
        [SerializeField] private FloatingText _healPrefab;
        [SerializeField] private FloatingText _critPrefab;

        [Header("动画参数")]
        [Tooltip("上飘速度（屏幕像素/秒）")]
        [SerializeField] private float _floatSpeed = 80f;
        [SerializeField] private float _fadeDuration = 1.2f;
        [SerializeField] private float _randomOffset = 0.4f;

        [Header("重击阈值")]
        [Tooltip("伤害 >= 此值视为重击，使用重击样式")]
        [SerializeField] private int _heavyDamageThreshold = 30;
        [Tooltip("重击颜色")]
        [SerializeField] private Color _heavyColor = new Color(1f, 0.3f, 0f); // 橙红
        [Tooltip("重击缩放倍率")]
        [SerializeField] private float _heavyScale = 1.5f;
        [Tooltip("普通伤害颜色")]
        [SerializeField] private Color _normalColor = Color.white;
        [Tooltip("命中点上方偏移（世界单位）")]
        [SerializeField] private float _popupHeightOffset = 1.5f;

        private ObjectPool<FloatingText> _damagePool;
        private ObjectPool<FloatingText> _healPool;
        private ObjectPool<FloatingText> _critPool;
        private FloatingText _defaultPrefabInstance; // 未拖入预制体时自动创建的模板

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            EnsureDefaultPrefab();

            _damagePool = CreatePool(_damagePrefab);
            _healPool = CreatePool(_healPrefab);
            _critPool = CreatePool(_critPrefab);
        }

        private void OnEnable()
        {
            CombatEvents.OnAttackHit += OnAttackHit;
        }

        private void OnDisable()
        {
            CombatEvents.OnAttackHit -= OnAttackHit;
        }

        private ObjectPool<FloatingText> CreatePool(FloatingText prefab)
        {
            return new ObjectPool<FloatingText>(
                () => Instantiate(prefab, transform),
                t => t.gameObject.SetActive(true),
                t => t.gameObject.SetActive(false),
                t => Destroy(t.gameObject),
                false, 10, 30
            );
        }

        /// <summary>在世界坐标位置弹出伤害数字（数值驱动样式）</summary>
        public void ShowDamage(Vector3 worldPos, int damage, bool isCrit = false)
        {
            bool isHeavy = damage >= _heavyDamageThreshold;
            var pool = isCrit ? _critPool : _damagePool;
            var ft = pool.Get();
            Color color = isCrit ? Color.yellow : (isHeavy ? _heavyColor : _normalColor);
            float scale = isHeavy ? _heavyScale : 1f;
            SetupFloatingText(ft, worldPos, damage.ToString(), color, scale);
        }

        /// <summary>弹出治疗数字</summary>
        public void ShowHeal(Vector3 worldPos, int amount)
        {
            var ft = _healPool.Get();
            SetupFloatingText(ft, worldPos, $"+{amount}", Color.green);
        }

        private void SetupFloatingText(FloatingText ft, Vector3 worldPos, string text, Color color, float scale = 1f)
        {
            ft.Initialize(worldPos, text, color, _floatSpeed, _fadeDuration,
                Random.insideUnitSphere * _randomOffset, scale);
            ft.OnComplete += ReturnToPool;
        }

        /// <summary>CombatEvents.OnAttackHit 回调 —— 在命中目标头顶弹出伤害数字（跳过玩家自身）</summary>
        private void OnAttackHit(Transform target, int damage)
        {
            if (target == null) return;
            // 不弹玩家受伤数字
            if (target.TryGetComponent<PlayerProperty>(out _)) return;
            if (target.GetComponentInParent<PlayerProperty>() != null) return;

            Vector3 popupPos = target.position + Vector3.up * _popupHeightOffset;
            ShowDamage(popupPos, damage);
        }

        /// <summary>未拖入预制体时自动创建一个最小可用的模板</summary>
        private void EnsureDefaultPrefab()
        {
            if (_damagePrefab != null) return;

            // 创建默认 FloatingText 模板（隐藏，仅作池工厂用）
            var go = new GameObject("DefaultFloatingText", typeof(RectTransform));
            go.transform.SetParent(transform, worldPositionStays: false);
            go.SetActive(false);

            var ft = go.AddComponent<FloatingText>();
            // FloatingText 内会通过 GetComponent 尝试获取 TextMeshProUGUI
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = 28;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.raycastTarget = false;
            // 添加描边
            var outlineComp = go.AddComponent<UnityEngine.UI.Outline>();
            outlineComp.effectColor = Color.black;
            outlineComp.effectDistance = new Vector2(1, -1);

            _defaultPrefabInstance = ft;
            _damagePrefab = ft;
            _healPrefab ??= ft;
            _critPrefab ??= ft;
        }

        private void ReturnToPool(FloatingText ft)
        {
            ft.OnComplete -= ReturnToPool;
            if (ft.IsCrit) _critPool.Release(ft);
            else if (ft.IsHeal) _healPool.Release(ft);
            else _damagePool.Release(ft);
        }
    }

    /// <summary>
    /// 单个浮动文字组件 —— 挂载在预制体上。
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public System.Action<FloatingText> OnComplete;
        public bool IsCrit { get; private set; }
        public bool IsHeal { get; private set; }

        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Outline _outline;

        private Vector3 _worldPos;
        private float _speed;
        private float _duration;
        private float _elapsed;
        private Vector3 _offset;
        private float _scale = 1f;

        private void Awake()
        {
            // 自动查找组件（支持运行时创建的默认预制体）
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_outline == null) _outline = GetComponent<Outline>();
        }

        public void Initialize(Vector3 worldPos, string text, Color color,
            float speed, float duration, Vector3 offset, float scale = 1f)
        {
            _worldPos = worldPos;
            _text.text = text;
            _text.color = color;
            _speed = speed;
            _duration = duration;
            _elapsed = 0f;
            _offset = offset;
            _scale = scale;
            IsCrit = color == Color.yellow;
            IsHeal = color == Color.green;

            // 应用缩放
            transform.localScale = Vector3.one * scale;

            // 世界坐标 → 屏幕坐标
            if (Camera.main != null)
                transform.position = Camera.main.WorldToScreenPoint(worldPos + _offset);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            transform.position += Vector3.up * _speed * Time.deltaTime;

            float alpha = 1f - (_elapsed / _duration);
            var c = _text.color;
            c.a = Mathf.Clamp01(alpha);
            _text.color = c;

            if (_elapsed >= _duration)
                OnComplete?.Invoke(this);
        }
    }
}
