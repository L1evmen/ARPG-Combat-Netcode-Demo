using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 按键设置页签 —— 从 SettingsManager.KeyBindings（JSON）读取所有动作，
/// 支持点击重绑定 + 冲突警告。currentPath 为空时显示 defaultPath。
/// </summary>
public class KeyBindingSettingsTab : MonoBehaviour
{
    [Header("PlayerInput 引用")]
    [SerializeField] private PlayerInput playerInput;

    [Header("行预制体")]
    [SerializeField] private GameObject bindingRowPrefab;

    [Header("行容器")]
    [SerializeField] private Transform rowsContainer;

    [Header("冲突警告文本（可选）")]
    [SerializeField] private TextMeshProUGUI conflictWarningText;

    [Header("恢复默认按钮")]
    [SerializeField] private Button resetKeysButton;

    public bool IsRebinding { get; private set; }

    private bool _justCancelledRebind;
    private readonly List<BindingRow> _rows = new List<BindingRow>();
    private BindingRow _currentRebindingRow;
    private string _previousPath;
    private Coroutine _rebindCoroutine;
    private float _rebindCooldownUntil;

    // ==================== 行数据 ====================

    private class BindingRow
    {
        public SettingsManager.KeyBindingEntry Entry;
        public TextMeshProUGUI actionLabel;
        public Button rebindButton;
        public TextMeshProUGUI keyLabel;
        public GameObject warningIcon;
        public GameObject Root;
    }

    // ==================== 生命周期 ====================

    private void Awake()
    {
        if (playerInput == null)
            Debug.LogWarning("[KeyBindingSettingsTab] PlayerInput 未赋值");
    }

    private void OnEnable()
    {
        BuildRows();
        RefreshAllRows();
        if (resetKeysButton != null)
            resetKeysButton.onClick.AddListener(OnResetKeys);
    }

    private void OnDisable()
    {
        CancelRebind();
        if (resetKeysButton != null)
            resetKeysButton.onClick.RemoveListener(OnResetKeys);
    }

    private void OnDestroy()
    {
        if (IsRebinding && playerInput != null)
            playerInput.enabled = true;
    }

    // ==================== 构建行列表 ====================

    private void BuildRows()
    {
        // 清理旧行
        foreach (var row in _rows)
        {
            if (row.rebindButton != null)
                row.rebindButton.onClick.RemoveAllListeners();
            if (row.Root != null)
                Destroy(row.Root);
        }
        _rows.Clear();

        var sm = SettingsManager.Instance;
        if (sm == null || bindingRowPrefab == null || rowsContainer == null) return;

        foreach (var entry in sm.KeyBindings)
        {
            GameObject go = Instantiate(bindingRowPrefab, rowsContainer);
            var row = new BindingRow { Entry = entry };
            row.Root = go;

            row.actionLabel = go.transform.Find("ActionLabel")?.GetComponent<TextMeshProUGUI>();
            row.rebindButton = go.transform.Find("RebindButton")?.GetComponent<Button>();
            row.keyLabel = go.transform.Find("RebindButton/KeyLabel")?.GetComponent<TextMeshProUGUI>();
            row.warningIcon = go.transform.Find("WarningIcon")?.gameObject;

            if (row.actionLabel != null)
                row.actionLabel.text = entry.displayName;

            if (row.rebindButton != null)
                row.rebindButton.onClick.AddListener(() => OnRebindClicked(row));

            _rows.Add(row);
        }
    }

    // ==================== 获取当前显示路径 ====================

    /// <summary>currentPath 为空 → 显示 defaultPath，否则显示 currentPath</summary>
    private string GetCurrentDisplayPath(SettingsManager.KeyBindingEntry entry)
    {
        return string.IsNullOrEmpty(entry.currentPath) ? entry.defaultPath : entry.currentPath;
    }

    // ==================== 改键流程 ====================

    private void OnRebindClicked(BindingRow row)
    {
        if (Time.unscaledTime < _rebindCooldownUntil) return;
        if (_currentRebindingRow == row && IsRebinding) return;
        if (IsRebinding) CancelRebind();

        _currentRebindingRow = row;
        _previousPath = GetCurrentDisplayPath(row.Entry);
        IsRebinding = true;

        if (playerInput != null)
            playerInput.enabled = false;

        if (row.keyLabel != null)
        {
            row.keyLabel.text = "按下新按键...";
            row.keyLabel.color = new Color(0.31f, 0.76f, 0.97f);
        }

        if (row.warningIcon != null)
            row.warningIcon.SetActive(false);

        _rebindCoroutine = StartCoroutine(WaitForKeyOrMouse());
    }

    private IEnumerator WaitForKeyOrMouse()
    {
        for (int safety = 0; safety < 60; safety++)
        {
            bool anyPressed = false;

            if (Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (key.isPressed) { anyPressed = true; break; }

            if (!anyPressed && Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed ||
                    Mouse.current.rightButton.isPressed ||
                    Mouse.current.middleButton.isPressed)
                    anyPressed = true;
            }

            if (!anyPressed && safety >= 2) break;
            yield return null;
        }

        while (IsRebinding)
        {
            if (Keyboard.current != null)
            {
                foreach (var key in Keyboard.current.allKeys)
                {
                    if (!key.wasPressedThisFrame) continue;
                    if (key == Keyboard.current.escapeKey) { CancelRebind(); yield break; }
                    ApplyRebind($"<Keyboard>/{key.name}");
                    yield break;
                }
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) { ApplyRebind("<Mouse>/leftButton"); yield break; }
                if (Mouse.current.rightButton.wasPressedThisFrame) { ApplyRebind("<Mouse>/rightButton"); yield break; }
                if (Mouse.current.middleButton.wasPressedThisFrame) { ApplyRebind("<Mouse>/middleButton"); yield break; }
                if (Mouse.current.forwardButton.wasPressedThisFrame) { ApplyRebind("<Mouse>/forwardButton"); yield break; }
                if (Mouse.current.backButton.wasPressedThisFrame) { ApplyRebind("<Mouse>/backButton"); yield break; }
            }

            yield return null;
        }
    }

    private void ApplyRebind(string newPath)
    {
        if (_currentRebindingRow == null) return;
        _currentRebindingRow.Entry.currentPath = newPath;
        SettingsManager.Instance?.SaveKeyBindings();

        // 即时应用到 Input System
        SettingsManager.Instance?.ApplyAllBindingOverrides(playerInput);

        RefreshAllRows();
        FinishRebind();
    }

    public void CancelRebind()
    {
        if (!IsRebinding) return;
        if (_currentRebindingRow?.keyLabel != null)
        {
            _currentRebindingRow.keyLabel.text = FormatKeyName(_previousPath);
            _currentRebindingRow.keyLabel.color = Color.white;
        }
        FinishRebind();
        _justCancelledRebind = true;
    }

    private void FinishRebind()
    {
        if (_rebindCoroutine != null) { StopCoroutine(_rebindCoroutine); _rebindCoroutine = null; }
        if (playerInput != null) playerInput.enabled = true;
        _rebindCooldownUntil = Time.unscaledTime + 0.3f;
        _currentRebindingRow = null;
        _previousPath = null;
        IsRebinding = false;
    }

    // ==================== UI 刷新 & 冲突检测 ====================

    private void RefreshAllRows()
    {
        // 基于当前有效路径检测冲突
        var pathActions = new Dictionary<string, List<string>>();
        foreach (var row in _rows)
        {
            string path = GetCurrentDisplayPath(row.Entry);
            if (string.IsNullOrEmpty(path)) continue;
            if (!pathActions.ContainsKey(path))
                pathActions[path] = new List<string>();
            pathActions[path].Add(row.Entry.action);
        }

        foreach (var row in _rows)
        {
            string displayPath = GetCurrentDisplayPath(row.Entry);

            if (row.keyLabel != null)
            {
                row.keyLabel.text = string.IsNullOrEmpty(displayPath) ? "未绑定" : FormatKeyName(displayPath);
                row.keyLabel.color = Color.white;
            }

            bool hasConflict = !string.IsNullOrEmpty(displayPath)
                            && pathActions.TryGetValue(displayPath, out var actions)
                            && actions.Count > 1;

            if (row.warningIcon != null)
                row.warningIcon.SetActive(hasConflict);
        }

        if (conflictWarningText != null)
        {
            bool anyConflict = pathActions.Values.Any(v => v.Count > 1);
            conflictWarningText.gameObject.SetActive(anyConflict);
            if (anyConflict)
                conflictWarningText.text = "存在按键冲突";
        }
    }

    // ==================== 格式化 ====================

    private string FormatKeyName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "未绑定";
        string n = path.TrimStart('/');
        n = NormalizeDevicePath(n);

        if (Matches(n, "<Mouse>/leftButton")) return "鼠标左键";
        if (Matches(n, "<Mouse>/rightButton")) return "鼠标右键";
        if (Matches(n, "<Mouse>/middleButton")) return "鼠标中键";
        if (n.Contains("Mouse") && n.EndsWith("Button", StringComparison.OrdinalIgnoreCase))
        {
            string btn = n.Substring(n.LastIndexOf('/') + 1).Replace("Button", "");
            return "鼠标" + btn;
        }

        if (Matches(n, "<Keyboard>/leftShift")) return "左Shift";
        if (Matches(n, "<Keyboard>/rightShift")) return "右Shift";
        if (Matches(n, "<Keyboard>/leftCtrl")) return "左Ctrl";
        if (Matches(n, "<Keyboard>/rightCtrl")) return "右Ctrl";
        if (Matches(n, "<Keyboard>/leftAlt")) return "左Alt";
        if (Matches(n, "<Keyboard>/rightAlt")) return "右Alt";
        if (Matches(n, "<Keyboard>/space")) return "空格";
        if (Matches(n, "<Keyboard>/enter")) return "回车";
        if (Matches(n, "<Keyboard>/numpadEnter")) return "小键盘回车";
        if (Matches(n, "<Keyboard>/escape")) return "Esc";
        if (Matches(n, "<Keyboard>/tab")) return "Tab";
        if (Matches(n, "<Keyboard>/backspace")) return "退格";
        if (Matches(n, "<Keyboard>/delete")) return "Delete";
        if (Matches(n, "<Keyboard>/insert")) return "Insert";
        if (Matches(n, "<Keyboard>/home")) return "Home";
        if (Matches(n, "<Keyboard>/end")) return "End";
        if (Matches(n, "<Keyboard>/pageUp")) return "Page Up";
        if (Matches(n, "<Keyboard>/pageDown")) return "Page Down";
        if (Matches(n, "<Keyboard>/capsLock")) return "Caps Lock";
        if (Matches(n, "<Keyboard>/numLock")) return "Num Lock";
        if (Matches(n, "<Keyboard>/upArrow")) return "↑";
        if (Matches(n, "<Keyboard>/downArrow")) return "↓";
        if (Matches(n, "<Keyboard>/leftArrow")) return "←";
        if (Matches(n, "<Keyboard>/rightArrow")) return "→";

        for (int i = 1; i <= 12; i++)
            if (Matches(n, $"<Keyboard>/f{i}")) return $"F{i}";

        if (n.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
        {
            string key = n.Substring(n.LastIndexOf('/') + 1);
            return key.Length == 1 ? key.ToUpper() : key;
        }

        int slash = n.LastIndexOf('/');
        return slash >= 0 ? n.Substring(slash + 1) : n;
    }

    private static bool Matches(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDevicePath(string path)
    {
        if (path.StartsWith("<") && path.Contains(">"))
        {
            int end = path.IndexOf('>');
            string device = path.Substring(1, end - 1);
            string rest = path.Substring(end + 1);
            return "<" + char.ToUpperInvariant(device[0]) + device.Substring(1) + ">" + rest;
        }
        return path;
    }

    // ==================== 重置 ====================

    private void OnResetKeys()
    {
        SettingsManager.Instance?.ResetKeyBindingsToDefault();
        if (playerInput != null)
            SettingsManager.Instance?.ApplyAllBindingOverrides(playerInput);
        RefreshAllRows();
    }

    // ==================== 公开方法 ====================

    public bool ConsumeRebindCancelledFlag()
    {
        bool was = _justCancelledRebind;
        _justCancelledRebind = false;
        return was;
    }

    public void OnPanelClosing()
    {
        if (IsRebinding) CancelRebind();
    }
}
