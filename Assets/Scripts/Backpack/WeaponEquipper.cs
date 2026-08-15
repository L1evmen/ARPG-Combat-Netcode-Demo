using UnityEngine;

/// <summary>只负责武器的显示：挂手 / 挂背 / 隐藏。支持刀鞘等副手配件。</summary>
[RequireComponent(typeof(Animator))]
public class WeaponEquipper : MonoBehaviour
{
    public Vector3 hipOffset = new Vector3(0, 0.15f, 0.1f);
    public Vector3 hipRotation = new Vector3(0, 180, 0);

    private Transform handBone;      // 右手
    private Transform offHandBone;   // 左手（刀鞘等副手配件）
    private Transform spineBone;
    private GameObject currentModel;
    private GameObject currentSheath;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        handBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
        offHandBone = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        spineBone = anim.GetBoneTransform(HumanBodyBones.Spine);
        WeaponManager.Instance.OnChanged += OnWeaponChanged;
        Debug.Log("WeaponEquipper Start: handBone=" + (handBone != null ? handBone.name : "NULL"));
    }

    void OnWeaponChanged()
    {
        var item = WeaponManager.Instance.GetCurrent();
        if (item == null) { Hide(); return; }
        EquipHand(item);
    }

    void OnDestroy() { if (WeaponManager.Instance != null) WeaponManager.Instance.OnChanged -= OnWeaponChanged; }

    public void EquipHand(ItemSO item)
    {
        HideSheath();

        if (currentModel != null) currentModel.SetActive(false);

        GameObject model = WeaponManager.Instance.GetModel(item);
        model.transform.SetParent(handBone);
        Weapon w = model.GetComponent<Weapon>();
        model.transform.localPosition = w != null ? w.gripPosition : item.gripPosition;
        model.transform.localRotation = Quaternion.Euler(w != null ? w.gripRotation : item.gripRotation);
        model.transform.localScale = w != null ? w.gripScale : item.gripScale;
        model.SetActive(true);
        var col = model.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var rb = model.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        currentModel = model;

        // 刀鞘：绑到左手
        EquipSheathIfExists(model);

        SetHoldItemId(item);
    }

    private void EquipSheathIfExists(GameObject weaponModel)
    {
        MeleeWeapon mw = weaponModel.GetComponent<MeleeWeapon>();
        if (mw == null || mw.SheathPrefab == null || offHandBone == null) return;

        currentSheath = Instantiate(mw.SheathPrefab, offHandBone);
        currentSheath.transform.localPosition = mw.SheathGripPos;
        currentSheath.transform.localRotation = Quaternion.Euler(mw.SheathGripRot);
        currentSheath.transform.localScale = mw.SheathGripScl;
        // 关闭鞘上的碰撞体（纯装饰）
        var cols = currentSheath.GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;
    }

    private void HideSheath()
    {
        if (currentSheath != null)
        {
            Destroy(currentSheath);
            currentSheath = null;
        }
    }

    public void EquipBack(ItemSO item)
    {
        HideSheath();

        if (currentModel != null) currentModel.SetActive(false);

        GameObject model = WeaponManager.Instance.GetModel(item);
        model.transform.SetParent(spineBone);
        model.transform.localPosition = hipOffset;
        model.transform.localRotation = Quaternion.Euler(hipRotation);
        model.SetActive(true);
        var col = model.GetComponent<Collider>();
        if (col) col.enabled = false;
        currentModel = model;
    }

    public void Hide()
    {
        HideSheath();
        if (currentModel != null) currentModel.SetActive(false);
        currentModel = null;
        SetHoldItemId(null);
    }

    void SetHoldItemId(ItemSO item)
    {
        if (anim == null) return;

        int id = 0;
        if (item != null)
        {
            if (item.itemType == ItemType.Consumable)
            {
                id = 4;
            }
            else if (item.prefab != null)
            {
                Weapon w = item.prefab.GetComponent<Weapon>();
                if (w is ScytheWeapon) id = 1;
                else if (w is JavelinWeapon) id = 2;
            }
        }
        anim.SetInteger("HoldItemId", id);
    }
}
