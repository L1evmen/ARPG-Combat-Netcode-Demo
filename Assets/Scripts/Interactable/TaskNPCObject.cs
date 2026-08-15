using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskNPCObject : InteractableObject
{
    public string npcName;
    public GameTaskSO gameTaskSO;

    public string[] contentInTaskExecuting;
    public string[] contentInTaskCompleted;
    public string[] contentInTaskEnd;

    [Header("对话摄像机")]
    [SerializeField] private Transform dialogueCamPoint;

    private void Start()
    {
        // 读档恢复时跳过重置（SaveManager.OnSceneLoaded 先于 Start 恢复了状态）
        bool isContinue = SaveManager.Instance != null
                       && GameManager.Instance != null
                       && !GameManager.Instance.IsNewGame;

        if (isContinue)
        {
            if (gameTaskSO.state == GameTaskState.Executing)
                gameTaskSO.ResumeTracking();
            return;
        }

        // 新游戏：重置任务状态（ScriptableObject 跨 Play 会话会残留旧值）
        Debug.Log($"[TaskNPCObject] Start: 重置前 state={gameTaskSO.state}, count={gameTaskSO.currentEnemyCount}");
        gameTaskSO.state = GameTaskState.Waiting;
        gameTaskSO.currentEnemyCount = 0;
        Debug.Log($"[TaskNPCObject] Start: 重置后 state={gameTaskSO.state}");
    }

    public override void Interact()
    {
        var cam = Camera.main.GetComponent<TPSCameraController>();
        cam.SetDialogueCamera(dialogueCamPoint);

        switch (gameTaskSO.state)
        {
            case GameTaskState.Waiting:
                DialogueUI.Instance.Show(npcName, gameTaskSO.diague, ComposeCallback(OnDialogueEnd, cam));
                break;
            case GameTaskState.Executing:
                DialogueUI.Instance.Show(npcName, contentInTaskExecuting, cam.ClearDialogueCamera);
                break;
            case GameTaskState.Completed:
                DialogueUI.Instance.Show(npcName, contentInTaskCompleted, ComposeCallback(OnDialogueEnd, cam));
                break;
            case GameTaskState.End:
                DialogueUI.Instance.Show(npcName, contentInTaskEnd, cam.ClearDialogueCamera);
                break;
            default:
                break;
        }
    }

    private Action ComposeCallback(Action original, TPSCameraController cam)
    {
        return () => { original?.Invoke(); cam.ClearDialogueCamera(); };
    }

    public void OnDialogueEnd()
    {
        switch (gameTaskSO.state)
        {
            case GameTaskState.Waiting:
                gameTaskSO.Start();
                GameInventory.InventoryService.Instance.AddItem(gameTaskSO.startReward);
                MessageUI.Instance.Show("你获得了一个物品，按Tab打开/关闭背包");
                break;
            case GameTaskState.Executing:
                break;
            case GameTaskState.Completed:
                gameTaskSO.End();
                GameInventory.InventoryService.Instance.AddItem(gameTaskSO.endReward);
                MessageUI.Instance.Show("奖励已放入背包");
                break;
            case GameTaskState.End:
                break;
            default:
                break;
        }


    }
}
