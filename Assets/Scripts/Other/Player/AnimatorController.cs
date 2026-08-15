using UnityEngine;

public class AnimatorController : MonoBehaviour
{
    Animator animator;
    public static int InputX = Animator.StringToHash("inputX");
    public static int InputY = Animator.StringToHash("inputY");
    public static int Status = Animator.StringToHash("status");
    public static int StopSpeed = Animator.StringToHash("stopSpeed");
    public static int StandRunToStop = Animator.StringToHash("StandRunToStop");
    public static int StandWalkToStop = Animator.StringToHash("StandWalkToStop");
    public static int StandSprintToStop = Animator.StringToHash("StandSprintToStop");
    public static int StandMove = Animator.StringToHash("StandMove");
    public static int CrouchMove = Animator.StringToHash("CrouchMove");
    public static int Slide = Animator.StringToHash("Slide");
    public static int Stop = Animator.StringToHash("Stop");
    public static int IsAttack = Animator.StringToHash("IsAttack");
    public static int Dancing = Animator.StringToHash("Dancing");
    

    private void Start()
    {
        animator = GetComponent<Animator>();

    }
}
