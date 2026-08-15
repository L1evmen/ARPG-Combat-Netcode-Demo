using UnityEngine;

public class PlayerIdleProgression : MonoBehaviour
{
    public float sadThreshold = 25f;
    public float layingThreshold = 45f;

    private Animator animator;
    private TPSPlayerMovement playerMove;
    private PlayerAttack playerAttack;
    private float idleTimer;

    void Start()
    {
        animator = GetComponent<Animator>();
        playerMove = GetComponent<TPSPlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
    }

    void Update()
    {
        if (playerMove == null || playerAttack == null) return;

        if (playerMove.IsMoving() || playerAttack.IsAttacking())
        {
            idleTimer = 0f;
            animator.SetInteger("IdleStage", 0);
        }
        else
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= layingThreshold)
                animator.SetInteger("IdleStage", 2);
            else if (idleTimer >= sadThreshold)
                animator.SetInteger("IdleStage", 1);
        }
    }
}
