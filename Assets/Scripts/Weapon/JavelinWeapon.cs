using System.Collections;
using UnityEngine;

public class JavelinWeapon : Weapon
{
    // === 配置 ===
    public float throwDelay = 0.3f;
    public float bulletSpeed = 15f;
    public float aimDistance = 50f;
    public float recallSpeed = 25f;
    public Vector3 aimOffset = Vector3.zero;
    public GameObject bulletPrefab;
    public int atkValue = 30;

    // === 覆盖基类 ===
    public override string AnimTrigger => "IsThrowing";
    public override bool RequiresAiming => true;

    // === 内部状态 ===
    private GameObject body;       // 手上的标枪
    private GameObject thrownBody; // 投出的标枪（飞行中 / 插在目标上）
    private bool isRecalling;

    private Vector3 bodyLocalPos;
    private Quaternion bodyLocalRot;
    private Vector3 bodyLocalScale;

    private Animator playerAnim;
    private static readonly int ParamIsJavelinInHand = Animator.StringToHash("IsJavelinInHand");

    // ==================== 生命周期 ====================

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag(Tag.PLAYER);
        if (player != null) playerAnim = player.GetComponentInChildren<Animator>();
        CreateBody();
    }

    void Update()
    {
        if (!IsCurrentWeapon()) return;
        if (isRecalling) return;

        // 本体被外部销毁（如敌人死亡），自动补一支
        if (body == null && thrownBody == null)
        {
            CreateBody();
            return;
        }

        // 按住右键召回飞行中的标枪
        if (Input.GetMouseButton(1) && thrownBody != null)
        {
            StartCoroutine(RecallRoutine());
        }
    }

    // ==================== 公开接口 ====================

    public override void Attack()
    {
        if (body == null || isRecalling) return;
        StartCoroutine(ThrowRoutine());
    }

    // ==================== 内部方法 ====================

    bool IsCurrentWeapon()
    {
        var mgr = WeaponManager.Instance;
        if (mgr == null) return false;
        var item = mgr.GetCurrent();
        if (item == null || item.prefab == null) return false;
        return item.prefab.GetComponent<Weapon>() is JavelinWeapon;
    }

    void CreateBody()
    {
        body = Instantiate(bulletPrefab, transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;
        bodyLocalPos = body.transform.localPosition;
        bodyLocalRot = body.transform.localRotation;
        bodyLocalScale = body.transform.localScale;

        // 世界物品模式：标枪作为可拾取物
        if (tag == Tag.INTERACTABLE)
        {
            Destroy(body.GetComponent<JavelinBullet>());
            body.tag = Tag.INTERACTABLE;
            PickableObject po = body.AddComponent<PickableObject>();
            po.itemSO = GetComponent<PickableObject>().itemSO;
            Rigidbody rb = body.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            Collider col = body.GetComponent<Collider>();
            col.enabled = true;
            col.isTrigger = false;
            body.transform.SetParent(null);
            Destroy(gameObject);
            return;
        }

        // 装备模式：挂手上，禁用物理
        body.GetComponent<Collider>().enabled = false;
        body.GetComponent<Rigidbody>().isKinematic = true;

        SetInHand(true);
    }

    Vector3 GetThrowDirection(Vector3 throwOrigin)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("JavelinWeapon: Camera.main 为空");
            return transform.forward;
        }

        Vector3 dir = cam.transform.forward;
        Debug.DrawRay(throwOrigin, dir * 5f, Color.red, 2f);
        return dir;
    }

    void SetInHand(bool value)
    {
        if (playerAnim != null)
            playerAnim.SetBool(ParamIsJavelinInHand, value);
    }

    // ==================== 投掷 ====================

    IEnumerator ThrowRoutine()
    {
        yield return new WaitForSeconds(throwDelay);

        if (body == null) yield break;

        GameObject obj = body;
        body = null;

        obj.transform.SetParent(null);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

        Vector3 throwDir = GetThrowDirection(obj.transform.position);
        obj.transform.forward = throwDir;
        rb.velocity = throwDir * bulletSpeed;

        obj.GetComponent<Collider>().enabled = true;

        JavelinBullet jb = obj.GetComponent<JavelinBullet>();
        if (jb != null) jb.atkValue = atkValue;

        thrownBody = obj;
        SetInHand(false);
    }

    // ==================== 召回 ====================

    IEnumerator RecallRoutine()
    {
        isRecalling = true;

        GameObject obj = thrownBody;
        thrownBody = null;

        // 从目标上脱钩
        obj.transform.SetParent(null);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Collider col = obj.GetComponent<Collider>();
        col.enabled = false;

        JavelinBullet jb = obj.GetComponent<JavelinBullet>();
        if (jb != null) jb.EnterRecallMode();

        // 飞回手中
        while (obj != null && Vector3.Distance(obj.transform.position, transform.position) > 0.3f)
        {
            Vector3 dir = (transform.position - obj.transform.position).normalized;
            obj.transform.position += dir * recallSpeed * Time.deltaTime;
            obj.transform.forward = dir;
            yield return null;
        }

        // 装回手上
        if (obj != null)
        {
            obj.transform.SetParent(transform);
            obj.transform.localPosition = bodyLocalPos;
            obj.transform.localRotation = bodyLocalRot;
            obj.transform.localScale = bodyLocalScale;
            col.enabled = false;
            rb.isKinematic = true;

            if (jb != null) jb.ExitRecallMode();

            body = obj;
            SetInHand(true);
        }

        isRecalling = false;
    }
}
