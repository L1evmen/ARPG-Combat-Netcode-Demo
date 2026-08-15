using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventCenter : MonoBehaviour
{
    public static event Action<int> OnEnemyDied;

    public static void EnemyDied(int exp)
    {
        Debug.Log($"[EventCenter] EnemyDied: 订阅者数量={OnEnemyDied?.GetInvocationList()?.Length ?? 0}");
        OnEnemyDied?.Invoke(exp);
    }
}
