using UnityEngine;

namespace GameHUD
{
    /// <summary>
    /// HUD 根管理器 —— 管理所有 HUD 面板的显隐与不同场景模式切换。
    ///
    /// 模式：Exploration（探索）、Combat（战斗）、Boss（头目）、Dialogue（对话）、Cutscene（播片）
    /// 每个子面板（血条、技能栏等）独立订阅事件，不依赖 HUDManager。
    /// HUDManager 仅负责全局显隐策略。
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        public enum HUDMode { Exploration, Combat, Boss, Dialogue, Cutscene }

        [Header("当前模式")]
        [SerializeField] private HUDMode _currentMode = HUDMode.Exploration;

        [Header("子面板引用（可选，通过 GetComponent 自动收集）")]
        [SerializeField] private PlayerStatusHUD _statusHUD;
        [SerializeField] private QuickItemBarHUD _quickItemBar;
        [SerializeField] private SkillBarHUD _skillBar;
        [SerializeField] private EnemyHealthBarHUD _enemyHealthBar;
        [SerializeField] private FloatingTextManager _floatingText;
        [SerializeField] private InteractionPromptHUD _interactionPrompt;
        [SerializeField] private ItemObtainToastHUD _itemToast;

        public HUDMode CurrentMode => _currentMode;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // 自动收集同对象上的子面板
            _statusHUD ??= GetComponentInChildren<PlayerStatusHUD>();
            _quickItemBar ??= GetComponentInChildren<QuickItemBarHUD>();
            _skillBar ??= GetComponentInChildren<SkillBarHUD>();
            _enemyHealthBar ??= GetComponentInChildren<EnemyHealthBarHUD>();
            _floatingText ??= GetComponentInChildren<FloatingTextManager>();
            _interactionPrompt ??= GetComponentInChildren<InteractionPromptHUD>();
            _itemToast ??= GetComponentInChildren<ItemObtainToastHUD>();
        }

        // ==================== 模式切换 ====================

        public void SetMode(HUDMode mode)
        {
            if (_currentMode == mode) return;
            _currentMode = mode;
            ApplyMode();
        }

        private void ApplyMode()
        {
            bool showStatus = true;
            bool showSkills = true;
            bool showQuickItems = true;
            bool showEnemyBar = false;
            float alpha = 1f;

            switch (_currentMode)
            {
                case HUDMode.Exploration:
                    showEnemyBar = false;
                    showSkills = false;
                    break;
                case HUDMode.Combat:
                    showEnemyBar = true;
                    break;
                case HUDMode.Boss:
                    showEnemyBar = true;
                    break;
                case HUDMode.Dialogue:
                    alpha = 0.3f;
                    showEnemyBar = false;
                    showSkills = false;
                    break;
                case HUDMode.Cutscene:
                    alpha = 0f;
                    break;
            }

            // 各面板独立响应（通过 CanvasGroup alpha 或 SetActive）
            SetPanelAlpha(_statusHUD, showStatus ? alpha : 0f);
            SetPanelAlpha(_quickItemBar, showQuickItems ? alpha : 0f);
            SetPanelAlpha(_skillBar, showSkills ? alpha : 0f);
            SetPanelAlpha(_enemyHealthBar, showEnemyBar ? 1f : 0f);
        }

        private void SetPanelAlpha(MonoBehaviour panel, float alpha)
        {
            if (panel == null) return;
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
            else panel.gameObject.SetActive(alpha > 0.01f);
        }

        private void Update()
        {
            // 战斗/非战斗状态自动切换示例
            // 实际项目可订阅 CombatEvents 来驱动模式切换
        }
    }
}
