using System;
using UnityEngine;

/// <summary>
/// 音效库 —— ScriptableObject，key → AudioClip + 默认音量。
/// 运行时通过 key 查找 clip，委托给 SFXPool 播放。
///
/// 创建方式：右键 → Create → Audio → SFX Library
/// </summary>
[CreateAssetMenu(fileName = "SFXLibrary", menuName = "Audio/SFX Library", order = 200)]
public class SFXLibrary : ScriptableObject
{
    public SFXEntry[] entries;

    /// <summary>按 key 查找 AudioClip，未找到返回 null</summary>
    public AudioClip FindClip(string key)
    {
        if (entries == null) return null;
        foreach (var e in entries)
        {
            if (e.key == key && e.clip != null)
                return e.clip;
        }
        return null;
    }

    /// <summary>按 key 查找默认音量，未找到返回 1f</summary>
    public float FindVolume(string key)
    {
        if (entries == null) return 1f;
        foreach (var e in entries)
        {
            if (e.key == key)
                return e.defaultVolume;
        }
        return 1f;
    }

    [Serializable]
    public class SFXEntry
    {
        [Tooltip("唯一标识，如 combat_swing, ui_button_click")]
        public string key;

        [Tooltip("音效资源")]
        public AudioClip clip;

        [Range(0f, 2f)]
        [Tooltip("基础音量倍率")]
        public float defaultVolume = 1f;
    }
}
