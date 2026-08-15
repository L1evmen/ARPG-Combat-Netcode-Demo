using System;
using System.Collections.Generic;

/// <summary>
/// 存档数据 —— 可序列化的游戏状态快照。
/// 所有 ScriptableObject 引用通过 id 字符串存储，读档时反查。
/// </summary>
[Serializable]
public class SaveData
{
    // ==================== 元信息 ====================
    public string saveTime;
    public string sceneName;
    public int slotIndex;

    // ==================== 玩家属性 ====================
    public int level;
    public int currentExp;
    public int hpValue;
    public int hpMax;
    public int energyValue;
    public int energyMax;
    public int mentalValue;
    public int mentalMax;

    // ==================== 玩家位置 ====================
    public float posX, posY, posZ;
    public float rotY;

    // ==================== 背包 ====================
    public List<InventoryEntry> inventory = new List<InventoryEntry>();

    // ==================== 装备栏 ====================
    public List<EquipmentEntry> equipment = new List<EquipmentEntry>();

    // ==================== 快捷栏 ====================
    public List<string> quickSlots = new List<string>();

    // ==================== 任务 ====================
    public List<TaskEntry> tasks = new List<TaskEntry>();

    // ==================== 已解锁技能 ====================
    public List<string> unlockedSkills = new List<string>();

    // ==================== 子结构 ====================

    [Serializable]
    public class InventoryEntry
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class EquipmentEntry
    {
        public string slotType;
        public string itemId; // 空 = 该槽无装备
    }

    [Serializable]
    public class TaskEntry
    {
        public string taskName;
        public int state;       // GameTaskState 枚举值
        public int currentEnemyCount;
    }
}
