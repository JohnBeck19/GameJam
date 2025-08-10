using System.Collections;
using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
   [SerializeField] protected float dmg;
   [SerializeField] float coolDown;
    bool used;
    public virtual void Use()
    {
        if (!used)
        {
            used = true;
            StartCoroutine(ResetAtk());
        }
        

    }

    IEnumerator ResetAtk()
    {
        yield return new WaitForSeconds(coolDown);
        used = false;
    }

}
