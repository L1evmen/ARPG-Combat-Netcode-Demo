using System.Collections.Generic;
using CombatV2;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public Weapon weapon;
    public Sprite weaponIcon;
    public int equippedWeaponIndex = -1;

    public Animator animator;
    private TPSCameraController camCtrl;
    private Transform rightHand;
    private ComboManager _comboManager;

    // 缓存实例化的武器，不销毁
    private Dictionary<ItemSO, GameObject> itemInstances = new Dictionary<ItemSO, GameObject>();
    private ItemSO currentItem;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
        _comboManager = GetComponent<ComboManager>();
        camCtrl = Camera.main?.GetComponent<TPSCameraController>();
        if (animator != null)
        {
            Debug.Log("PlayerAttack using Animator on: " + animator.gameObject.name + " | controller: " + animator.runtimeAnimatorController?.name);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        else
        {
            Debug.LogError("PlayerAttack: No Animator found!");
        }
    }

    void Attack()
    {
        animator.SetTrigger(weapon.AnimTrigger);
        weapon.Attack();
    }

    public void EquipWeaponFromInventory(int slotIndex)
    {
        var allItems = GameInventory.InventoryService.Instance.GetAllItemSOs();
        ItemSO target = null;
        int weaponIdx = 0;
        foreach (var it in allItems)
        {
            if (it.itemType == ItemType.Weapon)
            {
                if (weaponIdx == slotIndex) { target = it; break; }
                weaponIdx++;
            }
        }
        if (target == null) return;
        if (currentItem == target) return;

        HideCurrentItem();

        GameObject go = GetOrCreateItem(target);
        AttachToHand(go);

        weapon = go.GetComponent<Weapon>();
        weaponIcon = target.icon;
        currentItem = target;
        equippedWeaponIndex = slotIndex;
        _comboManager?.RefreshWeapon(go);
        PlayerPropertyUI.Instance?.UpdateUI();
    }

    void HideCurrentItem()
    {
        if (currentItem != null && itemInstances.ContainsKey(currentItem))
            itemInstances[currentItem].SetActive(false);
    }

    GameObject GetOrCreateItem(ItemSO item)
    {
        if (!itemInstances.ContainsKey(item))
        {
            GameObject go = Instantiate(item.prefab);
            go.SetActive(false);
            itemInstances[item] = go;
        }
        return itemInstances[item];
    }

    void AttachToHand(GameObject go)
    {
        Debug.Log("AttachToHand: go=" + go.name + " rightHand=" + (rightHand != null ? rightHand.name : "NULL"));
        go.transform.SetParent(rightHand);
        Weapon w = go.GetComponent<Weapon>();
        go.transform.localPosition = w != null ? w.gripPosition : Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(w != null ? w.gripRotation : Vector3.zero);
        go.transform.localScale = w != null ? w.gripScale : Vector3.one;
        go.SetActive(true);
        Debug.Log("AttachToHand done: active=" + go.activeSelf + " parent=" + go.transform.parent.name);
        var col = go.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    public void LoadWeapon(ItemSO itemSO)
    {
        HideCurrentItem();
        GameObject go = GetOrCreateItem(itemSO);
        AttachToHand(go);
        weapon = go.GetComponent<Weapon>();
        weaponIcon = itemSO.icon;
        currentItem = itemSO;
        _comboManager?.RefreshWeapon(go);

        var allItems = GameInventory.InventoryService.Instance.GetAllItemSOs();
        equippedWeaponIndex = -1;
        int wIdx = 0;
        foreach (var it in allItems)
        {
            if (it.itemType == ItemType.Weapon)
            {
                if (it == itemSO) { equippedWeaponIndex = wIdx; break; }
                wIdx++;
            }
        }
        PlayerPropertyUI.Instance?.UpdateUI();
    }

    public void UnloadWeapon()
    {
        weapon = null;
    }

    public string GetCurrentWeaponName()
    {
        if (weaponIcon != null && equippedWeaponIndex >= 0)
            return GameInventory.InventoryService.Instance.GetWeapons()[equippedWeaponIndex].name;
        return "无";
    }

    /// <summary>新武器系统调用的统一入口</summary>
    public void Execute(ItemSO item, bool isPotion)
    {
        if (isPotion)
        {
            // 消耗品使用已由 PlayerStateMachine.Drink 处理
            return;
        }

        if (item.prefab != null)
        {
            Weapon w = WeaponManager.Instance.GetModel(item).GetComponent<Weapon>();
            if (w != null)
            {
                this.weapon = w;
                if (animator != null)
                {
                    int hash = Animator.StringToHash(w.AnimTrigger);
                    animator.SetTrigger(hash);
                }
                w.Attack();
            }
        }
    }

    public bool IsAttacking()
    {
        return animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
    }
}
