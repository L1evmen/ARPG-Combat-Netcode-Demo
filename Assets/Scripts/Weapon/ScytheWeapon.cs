using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScytheWeapon : Weapon
{
    public int atkValue = 50;
    public float hitDelay = 0.25f;
    public float hitWindow = 0.2f;

    private Collider weaponCollider;
    private HashSet<Collider> hitTargets = new HashSet<Collider>();

    private void Start()
    {
        weaponCollider = GetComponentInChildren<Collider>();
        if (weaponCollider == null)
        {
            Debug.LogError("ScytheWeapon: No Collider found on " + gameObject.name);
            return;
        }
        weaponCollider.enabled = false;
        weaponCollider.isTrigger = true;

        Rigidbody rb = GetComponentInChildren<Rigidbody>();
        if (rb == null)
        {
            rb = weaponCollider.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }
        Debug.Log("ScytheWeapon ready: collider=" + weaponCollider.name + " isTrigger=" + weaponCollider.isTrigger + " hasRigidbody=" + (rb != null));
    }

    public override void Attack()
    {
        if (weaponCollider == null) { Debug.LogError("ScytheWeapon.Attack: weaponCollider is null!"); return; }
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        if (weaponCollider == null) yield break;
        yield return new WaitForSeconds(hitDelay);
        weaponCollider.enabled = true;
        Debug.Log("ScytheWeapon: collider ENABLED, pos=" + transform.position);
        yield return new WaitForSeconds(hitWindow);
        weaponCollider.enabled = false;
        Debug.Log("ScytheWeapon: collider DISABLED");
        hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("ScytheWeapon OnTriggerEnter: " + other.name + " tag=" + other.tag);
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (!other.CompareTag(Tag.ENEMY))
        {
            Debug.Log("  TryHit: tag mismatch, expected " + Tag.ENEMY + " got " + other.tag);
            return;
        }
        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || !damageable.CanBeHit())
        {
            Debug.Log("  TryHit: no IDamageable component on " + other.name);
            return;
        }
        if (hitTargets.Contains(other))
        {
            Debug.Log("  TryHit: already hit this enemy");
            return;
        }

        hitTargets.Add(other);
        damageable.TakeDamage(atkValue, transform);
        Debug.Log("  TryHit: HIT! damage=" + atkValue);
    }
}
