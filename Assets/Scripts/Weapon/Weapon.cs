using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public int attackValue;
    public Vector3 gripPosition = Vector3.zero;
    public Vector3 gripRotation = Vector3.zero;
    public Vector3 gripScale = Vector3.one;
    public virtual string AnimTrigger => "IsAttack";
    public virtual bool RequiresAiming => false;

    public virtual void Attack()
    {

    }
}
//test