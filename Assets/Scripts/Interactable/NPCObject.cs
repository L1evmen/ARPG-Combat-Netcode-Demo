using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCObject : InteractableObject
{
    public string npcName;
    public string[] contentList;

    [Header("对话摄像机")]
    [SerializeField] private Transform dialogueCamPoint;

    public override void Interact()
    {
        var cam = Camera.main.GetComponent<TPSCameraController>();
        cam.SetDialogueCamera(dialogueCamPoint);
        DialogueUI.Instance.Show(npcName, contentList, cam.ClearDialogueCamera);
    }
}
