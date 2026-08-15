using System.Collections.Generic;
using UnityEngine;
using GameHUD;

public class PlayerProperty : MonoBehaviour, IDamageable
{
    public Dictionary<PropertyType, List<Property>> propertyDict;
    public int hpValue = 100;
    public int hpMax = 100;
    public int energyValue = 100;
    public int energyMax = 100;
    public int mentalValue = 100;
    public int mentalMax = 100;
    [Header("耐力回复")]
    [Tooltip("每秒回复的耐力值")]
    [SerializeField] private float _staminaRecoveryRate = 15f;
    [Tooltip("耐力消耗后多久开始回复（秒）")]
    [SerializeField] private float _staminaRecoveryDelay = 1.5f;
    private float _lastStaminaUseTime;
    private float _staminaRecoveryAccum;
    public int level = 1;
    public int currentExp = 0;

    private PlayerStatusHUD _statusHUD;

    // Start is called before the first frame update
    void Awake()
    {
        _statusHUD = GetComponent<PlayerStatusHUD>() ?? FindObjectOfType<PlayerStatusHUD>();

        // 添加 Kinematic Rigidbody，使 HitBox 的 OnTriggerEnter 能检测到玩家
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        propertyDict = new Dictionary<PropertyType, List<Property>>();
        
        propertyDict.Add(PropertyType.SpeedValue, new List<Property>());
        propertyDict.Add(PropertyType.AttackValue, new List<Property>());

        AddProperty(PropertyType.SpeedValue, 5);
        AddProperty(PropertyType.AttackValue, 20);

        EventCenter.OnEnemyDied += OnEnemyDied;
    }

    public void UseDrug(ItemSO itemSO)
    {
        foreach(Property p in itemSO.propertyList)
        {
            AddProperty(p.propertyType, p.value);
        }
        PlayerPropertyUI.Instance?.UpdateUI();
    }

    public void TakeDamage(int damage)
    {
        if (CombatHitRouter.IsNetworkSession) return;

        Debug.Log($"[PlayerProperty] TakeDamage dmg={damage} hp={hpValue}->{hpValue - damage} iframe={GetComponent<PlayerStateMachine>()?.IsInIframe()}");
        hpValue -= damage;
        PlayerPropertyUI.Instance?.UpdateUI();
        _statusHUD?.SetHP(hpValue, hpMax);
        GetComponent<PlayerStateMachine>()?.OnAttacked(damage);
        if (hpValue <= 0)
            hpValue = hpMax;
    }

    /// <summary>IDamageable 接口实现，带伤害来源（用于 Boss / HitBox 系统）。</summary>
    void IDamageable.TakeDamage(int damage, Transform source)
    {
        TakeDamage(damage);
    }

    public bool CanBeHit() => enabled && hpValue > 0 && !CombatHitRouter.IsNetworkSession;

    public void ApplyNetworkHealth(int health, bool playHitReaction)
    {
        int previousHealth = hpValue;
        hpValue = Mathf.Clamp(health, 0, hpMax);
        PlayerPropertyUI.Instance?.UpdateUI();
        _statusHUD?.SetHP(hpValue, hpMax);

        if (playHitReaction && hpValue < previousHealth && hpValue > 0)
            GetComponent<PlayerStateMachine>()?.OnAttacked(previousHealth - hpValue);
    }

    public void TakeMentalDamage(int damage)
    {
        mentalValue -= damage;
        if (mentalValue < 0) mentalValue = 0;
        _lastStaminaUseTime = Time.time;
        _staminaRecoveryAccum = 0f;
        PlayerPropertyUI.Instance?.UpdateUI();
        _statusHUD?.SetSP(mentalValue, mentalMax);
    }

    /// <summary>消耗法力（由技能系统调用）</summary>
    public void ConsumeEnergy(int cost)
    {
        energyValue -= cost;
        if (energyValue < 0) energyValue = 0;
        PlayerPropertyUI.Instance?.UpdateUI();
        _statusHUD?.SetMP(energyValue, energyMax);
    }

    /// <summary>消耗耐力（由攻击系统调用）</summary>
    public void ConsumeStamina(int cost)
    {
        mentalValue -= cost;
        if (mentalValue < 0) mentalValue = 0;
        _lastStaminaUseTime = Time.time;
        _staminaRecoveryAccum = 0f;
        PlayerPropertyUI.Instance?.UpdateUI();
        _statusHUD?.SetSP(mentalValue, mentalMax);
    }

    public void AddProperty(PropertyType pt,int value)
    {
        switch (pt)
        {
            case PropertyType.HPValue:
                hpValue += value;
                _statusHUD?.SetHP(hpValue, hpMax);
                return;
            case PropertyType.EnergyValue:
                energyValue += value;
                _statusHUD?.SetMP(energyValue, energyMax);
                return;
            case PropertyType.MentalValue:
                mentalValue += value;
                _statusHUD?.SetSP(mentalValue, mentalMax);
                return;
        }

        List<Property> list;
        propertyDict.TryGetValue(pt, out list);
        list.Add(new Property(pt,value));
    }
    public void RemoveProperty(PropertyType pt, int value)
    {
        switch (pt)
        {
            case PropertyType.HPValue:
                hpValue -= value;
                _statusHUD?.SetHP(hpValue, hpMax);
                return;
            case PropertyType.EnergyValue:
                energyValue -= value;
                _statusHUD?.SetMP(energyValue, energyMax);
                return;
            case PropertyType.MentalValue:
                mentalValue -= value;
                _statusHUD?.SetSP(mentalValue, mentalMax);
                return;
        }

        List<Property> list;
        propertyDict.TryGetValue(pt, out list);

        list.Remove(list.Find(x => x.value == value));
    }
    private void Update()
    {
        // 耐力自动回复
        if (mentalValue < mentalMax && Time.time - _lastStaminaUseTime >= _staminaRecoveryDelay)
        {
            _staminaRecoveryAccum += _staminaRecoveryRate * Time.deltaTime;
            int recover = Mathf.FloorToInt(_staminaRecoveryAccum);
            if (recover > 0)
            {
                _staminaRecoveryAccum -= recover;
                mentalValue = Mathf.Min(mentalMax, mentalValue + recover);
                _statusHUD?.SetSP(mentalValue, mentalMax);
            }
        }
    }

    private void OnDestroy()
    {
        EventCenter.OnEnemyDied -= OnEnemyDied;
    }
    private void OnEnemyDied(int exp)
    {
        this.currentExp += exp;

        if (currentExp >= level * 30)
        {
            currentExp -= level * 30;
            level++;
        }
        PlayerPropertyUI.Instance?.UpdatePlayerPropertyUI();
    }
}
