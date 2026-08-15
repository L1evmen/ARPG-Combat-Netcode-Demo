using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能管理器 —— 管理技能解锁状态 + 技能配置（CD、法力消耗等）。
///
/// 在 Inspector 的 skills 数组中配置每个技能的 ID、冷却、法力消耗。
/// 新增技能只需添加数组元素，无需改代码。
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [System.Serializable]
    public class SkillConfig
    {
        public string skillId;
        public float cooldown = 5f;
        public int manaCost = 20;
    }

    [Header("技能配置（在 Inspector 中添加）")]
    [SerializeField] private SkillConfig[] _skills = new SkillConfig[0];

    private readonly HashSet<string> _unlocked = new HashSet<string>();
    private Dictionary<string, SkillConfig> _configLookup;
    private readonly Dictionary<string, float> _cooldownEndTimes = new Dictionary<string, float>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        _configLookup = new Dictionary<string, SkillConfig>(_skills.Length);
        foreach (var cfg in _skills)
            if (!string.IsNullOrEmpty(cfg.skillId))
                _configLookup[cfg.skillId] = cfg;
    }

    /// <summary>检查技能是否已解锁</summary>
    public bool IsUnlocked(string skillId) => _unlocked.Contains(skillId);

    /// <summary>解锁技能并触发事件</summary>
    public void Unlock(string skillId)
    {
        if (_unlocked.Contains(skillId)) return;
        _unlocked.Add(skillId);
        CombatEvents.FireSkillUnlocked(skillId);
        Debug.Log($"[SkillManager] 技能已解锁: {skillId}");
    }

    /// <summary>获取技能冷却时间（秒）</summary>
    public float GetCooldown(string skillId)
        => _configLookup.TryGetValue(skillId, out var cfg) ? cfg.cooldown : 5f;

    /// <summary>获取技能法力消耗</summary>
    public int GetManaCost(string skillId)
        => _configLookup.TryGetValue(skillId, out var cfg) ? cfg.manaCost : 20;

    /// <summary>检查技能是否在冷却中</summary>
    public bool IsOnCooldown(string skillId)
        => _cooldownEndTimes.TryGetValue(skillId, out var endTime) && Time.time < endTime;

    /// <summary>手动启动冷却（由技能 OnEnter 调用）</summary>
    public void StartCooldown(string skillId)
    {
        float cd = GetCooldown(skillId);
        _cooldownEndTimes[skillId] = Time.time + cd;
    }

    /// <summary>获取所有已解锁技能的 ID 列表（用于存档）</summary>
    public List<string> GetUnlockedSkillIds() => new List<string>(_unlocked);
}
