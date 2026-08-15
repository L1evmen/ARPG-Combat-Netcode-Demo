using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameInventory;

/// <summary>
/// 存档管理器 —— 负责游戏状态的保存与加载。
///
/// 多槽位：save_0.json ~ save_N.json，存储在 Application.persistentDataPath。
/// 读档时通过 ItemDBSO 的 id 反查 ItemSO 引用。
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public const int MAX_SLOTS = 4;

    private int _currentSlot = -1;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "00-Mainmeun") return;

        // 进入游戏场景
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsNewGame)
            {
                _currentSlot = GameManager.Instance.SelectedSlot;
                Debug.Log($"[SaveManager] 新游戏，槽位 {_currentSlot}");
            }
            else
            {
                _currentSlot = GameManager.Instance.SelectedSlot;
                Load(_currentSlot);
                Debug.Log($"[SaveManager] 继续游戏，已从槽位 {_currentSlot} 读档");
            }
        }
    }

    // ==================== 存档路径 ====================

    private static string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"save_{slotIndex}.json");
    }

    public static bool HasSave(int slotIndex) => File.Exists(GetSavePath(slotIndex));

    public static SaveData GetSaveInfo(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch { return null; }
    }

    public static void DeleteSave(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
    }

    // ==================== 保存 ====================

    public void Save()
    {
        Save(_currentSlot);
    }

    public void Save(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return;

        var data = new SaveData();
        data.slotIndex = slotIndex;
        data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.sceneName = SceneManager.GetActiveScene().name;

        CollectPlayerData(data);
        CollectInventoryData(data);
        CollectEquipmentData(data);
        CollectQuickSlotData(data);
        CollectTaskData(data);
        CollectSkillData(data);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(slotIndex), json);
        Debug.Log($"[SaveManager] 已保存到槽位 {slotIndex}");
    }

    // ==================== 加载 ====================

    public void Load(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] 槽位 {slotIndex} 无存档");
            return;
        }

        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);

        RestorePlayerData(data);
        RestoreInventoryData(data);
        RestoreEquipmentData(data);
        RestoreQuickSlotData(data);
        RestoreTaskData(data);
        RestoreSkillData(data);

        Debug.Log($"[SaveManager] 已从槽位 {slotIndex} 读档");
    }

    // ==================== 数据收集 ====================

    private void CollectPlayerData(SaveData data)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var prop = player.GetComponent<PlayerProperty>();
        if (prop != null)
        {
            data.level = prop.level;
            data.currentExp = prop.currentExp;
            data.hpValue = prop.hpValue;
            data.hpMax = prop.hpMax;
            data.energyValue = prop.energyValue;
            data.energyMax = prop.energyMax;
            data.mentalValue = prop.mentalValue;
            data.mentalMax = prop.mentalMax;
        }

        var pos = player.transform.position;
        data.posX = pos.x;
        data.posY = pos.y;
        data.posZ = pos.z;
        data.rotY = player.transform.eulerAngles.y;
    }

    private void CollectInventoryData(SaveData data)
    {
        if (InventoryService.Instance == null) return;

        // 通过 GetAllItemSOs + GetItemCount 收集
        var allItems = InventoryService.Instance.GetAllItemSOs();
        var counted = new HashSet<string>();
        foreach (var item in allItems)
        {
            string id = item.id.ToString();
            if (counted.Contains(id)) continue;
            counted.Add(id);

            int count = InventoryService.Instance.GetItemCount(id);
            data.inventory.Add(new SaveData.InventoryEntry { itemId = id, count = count });
        }
    }

    private void CollectEquipmentData(SaveData data)
    {
        if (EquipmentManager.Instance == null) return;

        foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            var eq = EquipmentManager.Instance.GetEquipment(slotType);
            data.equipment.Add(new SaveData.EquipmentEntry
            {
                slotType = slotType.ToString(),
                itemId = eq?.Id ?? ""
            });
        }
    }

    private void CollectQuickSlotData(SaveData data)
    {
        if (QuickSlotManager.Instance == null) return;

        for (int i = 0; i < QuickSlotManager.SLOT_COUNT; i++)
        {
            var item = QuickSlotManager.Instance.GetSlotItem(i);
            data.quickSlots.Add(item != null ? item.id.ToString() : "");
        }
    }

    private void CollectTaskData(SaveData data)
    {
        var taskNPCs = FindObjectsOfType<TaskNPCObject>();
        foreach (var npc in taskNPCs)
        {
            if (npc.gameTaskSO == null) continue;
            data.tasks.Add(new SaveData.TaskEntry
            {
                taskName = npc.gameTaskSO.taskName,
                state = (int)npc.gameTaskSO.state,
                currentEnemyCount = npc.gameTaskSO.currentEnemyCount
            });
        }
    }

    private void CollectSkillData(SaveData data)
    {
        // SkillManager 的 _unlocked 是 private，需要暴露一个接口
        // 这里通过 SkillManager 提供的方法获取
        if (SkillManager.Instance == null) return;
        data.unlockedSkills = SkillManager.Instance.GetUnlockedSkillIds();
    }

    // ==================== 数据恢复 ====================

    private void RestorePlayerData(SaveData data)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // 位置
        player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
        player.transform.rotation = Quaternion.Euler(0, data.rotY, 0);

        var prop = player.GetComponent<PlayerProperty>();
        if (prop != null)
        {
            prop.level = data.level;
            prop.currentExp = data.currentExp;
            prop.hpValue = data.hpValue;
            prop.hpMax = data.hpMax;
            prop.energyValue = data.energyValue;
            prop.energyMax = data.energyMax;
            prop.mentalValue = data.mentalValue;
            prop.mentalMax = data.mentalMax;
        }
    }

    private void RestoreInventoryData(SaveData data)
    {
        if (InventoryService.Instance == null || ItemDBManager.Instance == null) return;

        var db = ItemDBManager.Instance.itemDB;
        foreach (var entry in data.inventory)
        {
            var itemSO = db.itemList.FirstOrDefault(i => i.id.ToString() == entry.itemId);
            if (itemSO == null)
            {
                Debug.LogWarning($"[SaveManager] 未找到物品 id={entry.itemId}");
                continue;
            }
            InventoryService.Instance.AddItem(itemSO, entry.count);
        }
    }

    private void RestoreEquipmentData(SaveData data)
    {
        if (EquipmentManager.Instance == null || ItemDBManager.Instance == null) return;

        var db = ItemDBManager.Instance.itemDB;
        foreach (var entry in data.equipment)
        {
            if (string.IsNullOrEmpty(entry.itemId)) continue;

            var itemSO = db.itemList.FirstOrDefault(i => i.id.ToString() == entry.itemId);
            if (itemSO == null) continue;

            // 通过 InventoryService 的适配器获取 IEquipment
            // 先加入背包，再装备
            InventoryService.Instance.AddItem(itemSO, 1);
            var allSlots = InventoryService.Instance.GetFilteredItems();
            var slot = allSlots.FirstOrDefault(s => s.Item?.Id == entry.itemId);
            if (slot?.Item is IEquipment eq)
                EquipmentManager.Instance.Equip(eq);
        }
    }

    private void RestoreQuickSlotData(SaveData data)
    {
        if (QuickSlotManager.Instance == null || ItemDBManager.Instance == null) return;

        var db = ItemDBManager.Instance.itemDB;
        for (int i = 0; i < data.quickSlots.Count && i < QuickSlotManager.SLOT_COUNT; i++)
        {
            if (string.IsNullOrEmpty(data.quickSlots[i])) continue;
            var itemSO = db.itemList.FirstOrDefault(item => item.id.ToString() == data.quickSlots[i]);
            if (itemSO != null)
                QuickSlotManager.Instance.AssignSlot(i, itemSO);
        }
    }

    private void RestoreTaskData(SaveData data)
    {
        var taskNPCs = FindObjectsOfType<TaskNPCObject>();
        foreach (var npc in taskNPCs)
        {
            if (npc.gameTaskSO == null) continue;

            var entry = data.tasks.FirstOrDefault(t => t.taskName == npc.gameTaskSO.taskName);
            if (entry == null) continue;

            npc.gameTaskSO.state = (GameTaskState)entry.state;
            npc.gameTaskSO.currentEnemyCount = entry.currentEnemyCount;

            // 如果任务正在执行中，重新订阅事件
            if (npc.gameTaskSO.state == GameTaskState.Executing)
            {
                // 通过反射或公开方法重新订阅
                npc.gameTaskSO.ResumeTracking();
            }
        }
    }

    private void RestoreSkillData(SaveData data)
    {
        if (SkillManager.Instance == null) return;
        foreach (var skillId in data.unlockedSkills)
            SkillManager.Instance.Unlock(skillId);
    }
}
