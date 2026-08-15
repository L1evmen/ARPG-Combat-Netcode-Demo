using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class InputController : MonoBehaviour
{
    PlayerInput playerInput;
    public Vector2 moveVector2;
    public bool isSprint;
    public bool isCtrl;
    public bool isCrouch;
    public bool isJump;
    public bool isAttack;
    public bool MovementBlocked; // 闪避等状态下外部系统禁止移动
    Animator animatior;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        animatior = GetComponent<Animator>();
    }

    void Start()
    {
        // 延迟一帧应用自定义按键绑定，避免干扰 Input System 初始化
        StartCoroutine(ApplyBindingsNextFrame());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H) && !IsUIOpen())
        {
            var fsm = GetComponent<PlayerStateMachine>();
            if (fsm != null) fsm.ToggleDancing();
        }
    }

    private System.Collections.IEnumerator ApplyBindingsNextFrame()
    {
        yield return null;
        SettingsManager.Instance?.ApplyAllBindingOverrides(playerInput);
    }

    private void OnEnable()
    {
        playerInput.onActionTriggered += OnActionTrigger;
    }

    void OnDisable()
    {
        playerInput.onActionTriggered -= OnActionTrigger;
    }

    bool IsUIOpen() =>
        (PausePanel.Instance != null && PausePanel.Instance.IsVisible) ||
        (GameInventory.InventoryController.Instance != null && GameInventory.InventoryController.Instance.IsOpen) ||
        (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible);

    private void OnActionTrigger(InputAction.CallbackContext context)
    {
        switch (context.action.name)
        {
            case "Move":
                moveVector2 = IsUIOpen() ? Vector2.zero : context.ReadValue<Vector2>();
                break;
            case "Ctrl":
                if (IsUIOpen()) break;
                isCtrl = !isCtrl;
                break;
            case "Sprint":
                isSprint = !IsUIOpen() && context.action.IsPressed();
                break;
            case "Crouch":
                if (IsUIOpen()) break;
                isCrouch = !isCrouch;
                break;
            case "Jump":
                isJump = !IsUIOpen() && context.action.IsPressed();
                break;
            case "Attack":
                if (context.phase != InputActionPhase.Performed) break;
                isAttack = true;
                if (!IsUIOpen()
                    && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                {
                    animatior.SetTrigger(AnimatorController.IsAttack);
                }
                break;
        }
    }
}
