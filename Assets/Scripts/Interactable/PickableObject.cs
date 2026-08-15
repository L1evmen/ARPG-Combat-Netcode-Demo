using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PickableObject : InteractableObject
{
    public ItemSO itemSO;

    protected override void OnBeforeCreatePrompt()
    {
        promptText = "按[E]拾取";
        promptRange = 3f;
        promptHeight = 1f;
        promptWorldSpace = true;
        promptFont = Resources.Load<TMP_FontAsset>("ICE SDF");
    }

    // 接触玩家时自动拾取
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(Tag.PLAYER))
        {
            PickUp();
        }
    }

    public override void Interact()
    {
        PickUp();
    }

    void PickUp()
    {
        if (itemSO == null) return;
        GameInventory.InventoryService.Instance.AddItem(itemSO);
        ReturnToPool();
    }

    void ReturnToPool()
    {
        var rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        gameObject.SetActive(false);
    }
}
