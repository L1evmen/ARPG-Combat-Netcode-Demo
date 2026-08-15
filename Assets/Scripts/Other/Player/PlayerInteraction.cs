using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactRange = 3f; // 扫描半径 3 米
    public PlayerInput playerInput;  // 记得在面板里拖拽赋值

    void Update()
    {
        // 1. 极简按键检测：只在按下 E 键的那一帧触发
        if (playerInput.actions["Interact"].WasPressedThisFrame())
        {
            // 2. 获取 3 米内的所有碰撞体
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactRange);

            // 用于记录“目前找到的最距离”和“对应的物体”
            InteractableObject closestObj = null;
            float minDistance = float.MaxValue; // 初始设为最大值

            // 3. 遍历扫描到的所有物体，进行测距比对
            foreach (Collider col in colliders)
            {
                InteractableObject interactObj = col.GetComponent<InteractableObject>();

                if (interactObj != null)
                {
                    // 计算玩家和这个物品之间的距离
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    
                    // 如果这个距离比之前记录的更近，就更新“最近记录”
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestObj = interactObj;
                    }
                }
            }

            // 4. 循环结束后，如果确实找到了最近的物体，就执行它的互动逻辑
            if (closestObj != null)
            {
                closestObj.Interact();
            }
        }
    }
}