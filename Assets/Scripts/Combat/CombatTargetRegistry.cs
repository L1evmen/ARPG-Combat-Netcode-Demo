using System.Collections.Generic;
using UnityEngine;

public interface ICombatTarget
{
    Transform TargetTransform { get; }
    bool CanBeTargeted { get; }
}

public static class CombatTargetRegistry
{
    private static readonly List<ICombatTarget> Targets = new List<ICombatTarget>(2);

    public static void Register(ICombatTarget target)
    {
        if (!Targets.Contains(target))
            Targets.Add(target);
    }

    public static void Unregister(ICombatTarget target)
    {
        Targets.Remove(target);
    }

    public static bool TryGetClosest(Vector3 position, out Transform closest)
    {
        closest = null;
        float closestDistance = float.MaxValue;

        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            ICombatTarget target = Targets[i];
            if (target == null || target.TargetTransform == null)
            {
                Targets.RemoveAt(i);
                continue;
            }

            if (!target.CanBeTargeted)
                continue;

            float distance = (target.TargetTransform.position - position).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = target.TargetTransform;
        }

        return closest != null;
    }
}
