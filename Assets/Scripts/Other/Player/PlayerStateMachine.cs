using CombatV2;
using GameInventory;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    [Header("── 轻重击阈值 ──")]
    [SerializeField] float _heavyHitThreshold = 30;

    [Header("── 受击动画时长（秒）──")]
    [SerializeField] float _hitStunDuration = 0.6f;
    [SerializeField] float _staggerDuration = 0.9f;
    [SerializeField] float _recoverDuration = 0.7f;

    [Header("── 恢复后无敌时间（秒）──")]
    [SerializeField] float _recoveryIframe = 0.5f;

    [Header("── 喝药 ──")]
    [SerializeField] float _drinkApplyTime = 0.5f;
    [SerializeField] float _drinkTotalDuration = 1.2f;

    enum State { Normal, HitStun, Stagger, Recover, Drink, Dancing }
    State _state;
    Animator _animator;
    PlayerController _playerController;
    PlayerAttack _playerAttack;
    InputController _inputController;
    ComboManager _comboManager;
    PlayerProperty _playerProperty;
    bool _playerAttackWasEnabled;
    float _exitTime;
    float _lastRecoveryTime = float.MinValue;

    // Drink 状态数据
    private ItemSO _drinkItem;
    private int _drinkSlotIndex = -1;
    private bool _drinkApplied;
    private Coroutine _drinkCoroutine;

    void Awake()
    {
        _animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>() ?? GetComponentInChildren<Animator>();
        _playerController = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>();
        _playerAttack = GetComponent<PlayerAttack>() ?? GetComponentInParent<PlayerAttack>();
        _inputController = GetComponent<InputController>() ?? GetComponentInParent<InputController>();
        _comboManager = GetComponent<ComboManager>() ?? GetComponentInParent<ComboManager>();
        _playerProperty = GetComponent<PlayerProperty>() ?? GetComponentInParent<PlayerProperty>();
    }

    public bool IsInIframe() => Time.time - _lastRecoveryTime < _recoveryIframe;

    public void OnAttacked(int damage)
    {
        Debug.Log($"[PlayerStateMachine] OnAttacked damage={damage} state={_state} iframe={IsInIframe()}");
        if (IsInIframe()) return;

        // Drink 状态被攻击打断：不消耗物品
        if (_state == State.Drink)
        {
            InterruptDrink();
            return;
        }

        State newState = damage >= _heavyHitThreshold ? State.Stagger : State.HitStun;
        if (newState == _state)
        {
            _exitTime = Time.time + (newState == State.HitStun ? _hitStunDuration : _staggerDuration);
            return;
        }
        EnterState(newState);
    }

    // ==================== Drink 状态 ====================

    /// <summary>进入喝药状态。slotIndex 为 QuickSlotManager 中的槽位索引。</summary>
    public void EnterDrink(ItemSO item, int slotIndex)
    {
        if (_state != State.Normal || item == null) return;

        _drinkItem = item;
        _drinkSlotIndex = slotIndex;
        _drinkApplied = false;
        EnterState(State.Drink);

        _drinkCoroutine = StartCoroutine(DrinkRoutine());
    }

    private System.Collections.IEnumerator DrinkRoutine()
    {
        // 等待到喝药生效帧
        yield return new WaitForSeconds(_drinkApplyTime);

        // 如果还没被打断，执行效果
        if (_state == State.Drink && !_drinkApplied)
        {
            ApplyDrink();
        }
    }

    private void ApplyDrink()
    {
        _drinkApplied = true;
        _playerProperty?.UseDrug(_drinkItem);
        InventoryService.Instance.RemoveItem(_drinkItem);

        // 数量归零则清空快捷栏槽位
        if (InventoryService.Instance.GetItemCount(_drinkItem) <= 0)
            QuickSlotManager.Instance.ClearSlot(_drinkSlotIndex);
        else
            QuickSlotManager.Instance.RefreshExistingSlot(_drinkItem);
    }

    private void InterruptDrink()
    {
        if (_drinkCoroutine != null)
        {
            StopCoroutine(_drinkCoroutine);
            _drinkCoroutine = null;
        }
        ReturnToNormal();
    }

    // ==================== Dancing 状态 ====================

    /// <summary>H 键切换跳舞状态</summary>
    public void ToggleDancing()
    {
        if (_state == State.Dancing)
        {
            ReturnToNormal();
            return;
        }
        if (_state != State.Normal) return;
        EnterState(State.Dancing);
    }

    // ==================== 状态机 ====================

    void EnterState(State s)
    {
        Debug.Log($"[PlayerStateMachine] EnterState {s}");
        if (_state == State.Normal && _playerAttack != null)
            _playerAttackWasEnabled = _playerAttack.enabled;
        _state = s;
        if (_playerController != null) _playerController.UseRootMotionY = false;
        if (_playerAttack != null) _playerAttack.enabled = false;
        if (_comboManager != null)
        {
            _comboManager.ForceIdle();
            _comboManager.enabled = false;
        }
        if (_inputController != null) _inputController.MovementBlocked = true;

        string animName = null;
        float duration = 0.6f;
        switch (s)
        {
            case State.HitStun: animName = "HitStun"; duration = _hitStunDuration; break;
            case State.Stagger: animName = "Stagger"; duration = _staggerDuration; break;
            case State.Recover: animName = "Recover"; duration = _recoverDuration; break;
            case State.Drink: animName = "Drink"; duration = _drinkTotalDuration; break;
            case State.Dancing: animName = "Dancing"; duration = float.MaxValue; break;
        }

        if (animName != null)
            _animator.CrossFadeInFixedTime(animName, 0.05f, 0);

        _exitTime = Time.time + duration;
    }

    void Update()
    {
        if (_state == State.Normal) return;

        if (Time.time >= _exitTime)
        {
            if (_state == State.Stagger)
                EnterState(State.Recover);
            else
                ReturnToNormal();
        }
    }

    void ReturnToNormal()
    {
        Debug.Log("[PlayerStateMachine] ReturnToNormal");
        bool wasRecover = _state == State.Recover;
        _state = State.Normal;
        if (wasRecover) _lastRecoveryTime = Time.time;
        _animator.CrossFadeInFixedTime("Standmove", 0.5f, 0);
        if (_inputController != null) _inputController.MovementBlocked = false;
        if (_playerAttack != null) _playerAttack.enabled = _playerAttackWasEnabled;
        if (_comboManager != null)
        {
            _comboManager.ForceIdle();
            _comboManager.enabled = true;
        }

        // 清理 Drink 数据
        _drinkItem = null;
        _drinkSlotIndex = -1;
        _drinkCoroutine = null;
    }
}
