using UnityEngine;

public class JavelinBullet : MonoBehaviour
{
    public int atkValue = 30;
    private Rigidbody rgd;
    private Collider col;
    private bool isRecalling;

    private void Start()
    {
        rgd = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void Update()
    {
        if (isRecalling) return;
        if (!rgd.isKinematic && rgd.velocity.sqrMagnitude > 0.01f)
            transform.forward = rgd.velocity.normalized;
    }

    public void EnterRecallMode()
    {
        isRecalling = true;
    }

    public void ExitRecallMode()
    {
        isRecalling = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isRecalling) return;
        if (collision.collider.CompareTag(Tag.PLAYER)) return;

        rgd.isKinematic = true;
        col.enabled = false;
        transform.parent = collision.gameObject.transform;

        if (collision.gameObject.CompareTag(Tag.ENEMY))
        {
            var damageable = collision.gameObject.GetComponent<IDamageable>();
            if (damageable != null && damageable.CanBeHit())
                damageable.TakeDamage(atkValue, transform);
        }
    }
}
