using CombatV2;
using GameInventory;
using UnityEngine;
using UnityEngine.EventSystems;

public class WeaponInputHandler : MonoBehaviour
{
    private PlayerAttack attack;
    private WeaponManager manager;
    private ComboManager _comboManager;
    private PlayerStateMachine _stateMachine;
    private float uiCloseCooldown;

    void Start()
    {
        attack = GetComponent<PlayerAttack>();
        if (attack == null)
            attack = gameObject.AddComponent<PlayerAttack>();
        attack.enabled = false;
        _comboManager = GetComponent<ComboManager>();
        if (_comboManager == null)
            _comboManager = gameObject.AddComponent<ComboManager>();
        _stateMachine = GetComponent<PlayerStateMachine>();
        manager = WeaponManager.Instance;
    }

    void Update()
    {
        if (IsUIOpen())
        {
            uiCloseCooldown = 0.15f;
            return;
        }

        if (uiCloseCooldown > 0f)
        {
            uiCloseCooldown -= Time.deltaTime;
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 数字键1-4：使用快捷栏消耗品
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryUseQuickSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryUseQuickSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryUseQuickSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TryUseQuickSlot(3);

        // 鼠标左键：使用当前武器攻击
        if (Input.GetMouseButtonDown(0) && manager.currentIndex >= 0)
        {
            ItemSO item = manager.GetCurrent();
            if (item == null) return;

            var model = WeaponManager.Instance.GetModel(item);
            Weapon w = model != null ? model.GetComponent<Weapon>() : null;
            if (w != null && w.RequiresAiming)
            {
                var cam = Camera.main?.GetComponent<TPSCameraController>();
                if (cam == null || !cam.IsAiming) return;
            }
            _comboManager.RefreshWeapon(model);
        }
    }

    void TryUseQuickSlot(int slotIndex)
    {
        if (QuickSlotManager.Instance == null) return;
        var item = QuickSlotManager.Instance.GetSlotItem(slotIndex);
        if (item == null) return;

        _stateMachine?.EnterDrink(item, slotIndex);
    }

    bool IsUIOpen() =>
        (PausePanel.Instance != null && PausePanel.Instance.IsVisible) ||
        (GameInventory.InventoryController.Instance != null && GameInventory.InventoryController.Instance.IsOpen) ||
        (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible);
}
