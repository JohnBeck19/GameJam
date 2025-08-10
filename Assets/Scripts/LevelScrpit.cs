using UnityEngine;

public class LevelScrpit : MonoBehaviour
{
    int currentLvl = 1;
    int currentExp = 0;
    public int expToNextLvl;
    public int nextLvlCap;
    public int maxLvl = 50;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Triggered");
        if (collision.gameObject.GetComponent<ExpScript>())
        {
            ExpScript exp = collision.gameObject.GetComponent<ExpScript>();
            LevelUp(exp.exp);
            exp.Comsumed();
        }
    }

    void LevelUp(int exp)
    {
        if (currentLvl != maxLvl)
        {
            if (currentExp < expToNextLvl)
            {
                currentExp += exp;
                Debug.Log(exp);
            }
            else
            {
                currentLvl += 1;
                expToNextLvl *= nextLvlCap;
                currentExp = 0;
            }
        }
    }
}
