using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatV2
{
    /// <summary>
    /// 默认战斗输入提供者 —— 直接从 Unity Input System 设备读取。
    ///
    /// 这是 ICombatInputProvider 的默认实现，硬编码了键位映射：
    ///   轻击   → 鼠标左键
    ///   重击   → 鼠标右键
    ///   上方向 → W / ↑
    ///   闪避   → 左 Shift
    ///   移动   → WASD
    ///
    /// 后续替换为 RebindableCombatInputProvider 即可实现键位重映射，
    /// 无需修改 ComboManager 或任何招式脚本。
    /// </summary>
    public class DefaultCombatInputProvider : MonoBehaviour, ICombatInputProvider
    {
        // ==================== ICombatInputProvider 实现 ====================

        public bool LightPressed { get; private set; }
        public bool HeavyPressed { get; private set; }
        public bool HeavyHeld { get; private set; }
        public bool HeavyReleased { get; private set; }
        public bool UpPressed { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool SpecialPressed { get; private set; }
        public Vector2 MoveInput { get; private set; }

        // ==================== 可选：通过 InputController 获取移动输入 ====================

        [Tooltip("如果不为空，MoveInput 从此组件读取（与角色移动保持一致）")]
        [SerializeField] private InputController _inputController;

        private void Awake()
        {
            if (_inputController == null)
                _inputController = GetComponent<InputController>();
        }

        /// <summary>由 ComboManager 在初始化时调用，设置 InputController 引用</summary>
        public void SetInputController(InputController ctrl)
        {
            _inputController = ctrl;
        }

        /// <summary>
        /// 每帧调用，刷新所有输入状态。
        /// 应在 ComboManager.Update() 最开始调用。
        /// </summary>
        public void UpdateInput()
        {
            if (IsInventoryOpen())
            {
                LightPressed = HeavyPressed = UpPressed = DodgePressed = SpecialPressed = false;
                HeavyHeld = false;
                HeavyReleased = false;
                MoveInput = Vector2.zero;
                return;
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null)
            {
                LightPressed = mouse.leftButton.wasPressedThisFrame;
                HeavyPressed = mouse.rightButton.wasPressedThisFrame;
                HeavyHeld = mouse.rightButton.isPressed;
                HeavyReleased = mouse.rightButton.wasReleasedThisFrame;
            }
            else
            {
                LightPressed = false;
                HeavyPressed = false;
                HeavyHeld = false;
                HeavyReleased = false;
            }

            if (keyboard != null)
            {
                UpPressed = keyboard.wKey.wasPressedThisFrame ||
                            keyboard.upArrowKey.wasPressedThisFrame;
                DodgePressed = keyboard.leftShiftKey.wasPressedThisFrame;
                SpecialPressed = keyboard.fKey.wasPressedThisFrame;
            }
            else
            {
                UpPressed = false;
                DodgePressed = false;
                SpecialPressed = false;
            }

            if (_inputController != null)
                MoveInput = _inputController.moveVector2;
            else
                MoveInput = Vector2.zero;
        }

        private static bool IsInventoryOpen()
        {
            return (PausePanel.Instance != null && PausePanel.Instance.IsVisible)
                || (GameInventory.InventoryController.Instance != null && GameInventory.InventoryController.Instance.IsOpen);
        }
    }
}
