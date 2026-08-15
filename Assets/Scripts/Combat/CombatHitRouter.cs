using UnityEngine;

public interface ICombatHitRouter
{
    bool TryRouteHit(IDamageable target, int damage, Transform source);
}

public static class CombatHitRouter
{
    private static ICombatHitRouter _router;

    public static bool IsNetworkSession => _router != null;

    public static void Register(ICombatHitRouter router)
    {
        _router = router;
    }

    public static void Unregister(ICombatHitRouter router)
    {
        if (_router == router)
            _router = null;
    }

    public static bool TryRoute(
        IDamageable target,
        int damage,
        Transform source)
    {
        return _router != null && _router.TryRouteHit(target, damage, source);
    }
}
