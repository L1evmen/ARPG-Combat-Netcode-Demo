using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// SkillBarHUD - v2 string-based skillId

namespace GameHUD
{
    /// <summary>
    /// 技能栏 HUD —— 屏幕右上方常驻。
    ///
    /// 每个槽位通过 skillId 关联一个技能，未解锁的技能完全隐藏。
    /// 解锁时播放闪光动画 + 弹窗提示。
    /// 新增技能只需在 Inspector 中添加槽位并填写 skillId。
    /// </summary>
    public class SkillBarHUD : MonoBehaviour
    {
        [System.Serializable]
        public struct SkillSlotUI
        {
            public string skillId;              // 技能 ID（与 SkillManager 对应）
            public string displayName;          // 解锁提示用的显示名
            public GameObject Root;             // 槽位根物体（控制显隐）
            public Image Icon;
            public Image CooldownOverlay;
            public TextMeshProUGUI CooldownText;
            public TextMeshProUGUI KeyHint;
            public GameObject ReadyGlow;
        }

        [Header("技能槽位（在 Inspector 中添加）")]
        [SerializeField] private SkillSlotUI[] _slots = new SkillSlotUI[0];

        private float[] _cooldownRemaining;
        private float[] _cooldownTotal;
        private Dictionary<string, int> _skillIdToIndex;

        private void OnEnable()
        {
            CombatEvents.OnSkillUnlocked += OnSkillUnlocked;
            CombatEvents.OnSkillUsed += OnSkillUsed;
        }

        private void OnDisable()
        {
            CombatEvents.OnSkillUnlocked -= OnSkillUnlocked;
            CombatEvents.OnSkillUsed -= OnSkillUsed;
        }

        private void Start()
        {
            int count = _slots.Length;
            _cooldownRemaining = new float[count];
            _cooldownTotal = new float[count];
            _skillIdToIndex = new Dictionary<string, int>(count);

            for (int i = 0; i < count; i++)
            {
                _cooldownRemaining[i] = 0f;
                _cooldownTotal[i] = 0f;

                if (!string.IsNullOrEmpty(_slots[i].skillId))
                    _skillIdToIndex[_slots[i].skillId] = i;

                // 未解锁 → 隐藏槽位
                bool unlocked = string.IsNullOrEmpty(_slots[i].skillId)
                    || (SkillManager.Instance != null
                        && SkillManager.Instance.IsUnlocked(_slots[i].skillId));
                SetSlotVisible(_slots[i], unlocked);
            }
        }

        private void OnSkillUnlocked(string skillId)
        {
            Debug.Log($"[SkillBarHUD] OnSkillUnlocked: skillId={skillId}, 查找结果={_skillIdToIndex.ContainsKey(skillId)}, slots数量={_slots.Length}, MessageUI={MessageUI.Instance != null}");
            if (!_skillIdToIndex.TryGetValue(skillId, out int index)) return;

            var slot = _slots[index];
            string name = string.IsNullOrEmpty(slot.displayName) ? skillId : slot.displayName;
            Debug.Log($"[SkillBarHUD] 显示槽位: displayName={slot.displayName}, 最终显示={name}");
            SetSlotVisible(slot, true);
            StartCoroutine(PlayUnlockAnimation(slot));
            MessageUI.Instance?.Show($"{name}已解锁！");
        }

        private void OnSkillUsed(string skillId)
        {
            if (!_skillIdToIndex.TryGetValue(skillId, out int index)) return;

            // 从 SkillManager 读取冷却时间并启动倒计时
            float cd = SkillManager.Instance != null
                ? SkillManager.Instance.GetCooldown(skillId) : 5f;
            _cooldownTotal[index] = cd;
            _cooldownRemaining[index] = cd;
        }

        private void SetSlotVisible(SkillSlotUI slot, bool visible)
        {
            var root = slot.Root ?? slot.Icon?.transform.parent?.gameObject;
            if (root != null) root.SetActive(visible);
        }

        private IEnumerator PlayUnlockAnimation(SkillSlotUI slot)
        {
            var root = slot.Root ?? slot.Icon?.transform.parent?.gameObject;
            if (root == null) yield break;

            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            root.transform.localScale = Vector3.one * 1.2f;

            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - (1f - t) * (1f - t);
                cg.alpha = ease;
                root.transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, ease);
                yield return null;
            }

            cg.alpha = 1f;
            root.transform.localScale = Vector3.one;
        }

        private void Update()
        {
            for (int i = 0; i < _slots.Length; i++)
                UpdateSkillSlot(_slots[i], ref _cooldownRemaining[i], _cooldownTotal[i]);
        }

        private void UpdateSkillSlot(SkillSlotUI slot, ref float remaining, float total)
        {
            if (slot.Icon == null) return;

            var root = slot.Root ?? slot.Icon?.transform.parent?.gameObject;
            if (root != null && !root.activeSelf) return;

            if (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                float ratio = total > 0f ? remaining / total : 0f;

                if (slot.CooldownOverlay != null)
                {
                    slot.CooldownOverlay.fillAmount = ratio;
                    slot.CooldownOverlay.enabled = true;
                }
                if (slot.CooldownText != null)
                {
                    slot.CooldownText.text = Mathf.CeilToInt(remaining).ToString();
                    slot.CooldownText.enabled = true;
                }
                if (slot.ReadyGlow != null)
                    slot.ReadyGlow.SetActive(false);
            }
            else
            {
                if (slot.CooldownOverlay != null)
                    slot.CooldownOverlay.enabled = false;
                if (slot.CooldownText != null)
                    slot.CooldownText.enabled = false;
                if (slot.ReadyGlow != null)
                    slot.ReadyGlow.SetActive(true);
            }
        }

        // ==================== 公开 API ====================

        /// <summary>根据 skillId 查找槽位的显示名，找不到返回 null</summary>
        public string GetSlotDisplayName(string skillId)
        {
            if (_skillIdToIndex != null
                && _skillIdToIndex.TryGetValue(skillId, out int i)
                && !string.IsNullOrEmpty(_slots[i].displayName))
                return _slots[i].displayName;
            return null;
        }

        /// <summary>根据 skillId 查找槽位的图标，找不到返回 null</summary>
        public Sprite GetSlotIcon(string skillId)
        {
            if (_skillIdToIndex != null
                && _skillIdToIndex.TryGetValue(skillId, out int i)
                && _slots[i].Icon != null)
                return _slots[i].Icon.sprite;
            return null;
        }

        /// <summary>设置指定技能的冷却</summary>
        public void SetCooldown(string skillId, float cooldownSeconds)
        {
            if (!_skillIdToIndex.TryGetValue(skillId, out int i)) return;
            _cooldownTotal[i] = cooldownSeconds;
            _cooldownRemaining[i] = cooldownSeconds;
        }

        /// <summary>启动指定技能的冷却倒计时</summary>
        public void StartCooldown(string skillId)
        {
            if (!_skillIdToIndex.TryGetValue(skillId, out int i)) return;
            _cooldownRemaining[i] = _cooldownTotal[i];
        }

        /// <summary>设置指定技能的图标</summary>
        public void SetIcon(string skillId, Sprite icon)
        {
            if (!_skillIdToIndex.TryGetValue(skillId, out int i)) return;
            if (_slots[i].Icon != null) _slots[i].Icon.sprite = icon;
        }
    }
}
