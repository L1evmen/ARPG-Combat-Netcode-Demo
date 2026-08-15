using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameTaskState
{
    Waiting,
    Executing,
    Completed,
    End
}
[CreateAssetMenu()]
public class GameTaskSO:ScriptableObject
{
    public GameTaskState state;

    public string taskName;
    public string taskDescription;
    public string[] diague;

    public ItemSO startReward;
    public ItemSO endReward;

    [Header("任务完成解锁的技能（填写技能 ID，如 RisingSlash）")]
    public string[] skillsToUnlock;

    public int enemyCountNeed = 10;

    public int currentEnemyCount = 0;

    public void Start()
    {
        currentEnemyCount = 0;
        state = GameTaskState.Executing;
        if (PlayerPropertyUI.Instance != null)
            PlayerPropertyUI.Instance.activeTask = this;
        EventCenter.OnEnemyDied += OnEnemyDied;
        Debug.Log($"[GameTaskSO] Start: state={state}, 已订阅 OnEnemyDied");
    }

    private void OnEnemyDied(int exp)
    {
        Debug.Log($"[GameTaskSO] OnEnemyDied: state={state}, count={currentEnemyCount}/{enemyCountNeed}");
        if (state == GameTaskState.Completed) return;
        currentEnemyCount++;
        if(currentEnemyCount>= enemyCountNeed)
        {
            state = GameTaskState.Completed;
            MessageUI.Instance.Show("任务已完成，回去看看吧");
        }
    }


    public void End()
    {
        state = GameTaskState.End;
        EventCenter.OnEnemyDied -= OnEnemyDied;

        // 解锁任务关联的技能
        if (skillsToUnlock != null && SkillManager.Instance != null)
        {
            foreach (var skillId in skillsToUnlock)
                SkillManager.Instance.Unlock(skillId);
        }
    }

    /// <summary>读档后恢复正在执行的任务，重新订阅击杀事件</summary>
    public void ResumeTracking()
    {
        EventCenter.OnEnemyDied -= OnEnemyDied; // 防止重复订阅
        EventCenter.OnEnemyDied += OnEnemyDied;
        if (PlayerPropertyUI.Instance != null)
            PlayerPropertyUI.Instance.activeTask = this;
    }
}
