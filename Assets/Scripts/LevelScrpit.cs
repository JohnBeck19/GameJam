using UnityEngine;

public class LevelScrpit : MonoBehaviour
{
    int currentLvl = 1;
    int currentExp = 0;
    public int expToNextLvl;
    public int nextLvlCap;
    public int maxLvl = 50;

    public CardSelectionManager cardManager;

    // Keep collision-based pickup support in case EXP uses non-trigger colliders
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        var exp = collision.gameObject.GetComponent<ExpScript>();
        if (exp != null)
        {
            AddExp(exp.exp);
            exp.Comsumed();
        }
    }

    // Public entry so EXP or other systems can grant XP without requiring physics collisions
    public void AddExp(int amount)
    {
        LevelUp(amount);
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
                cardManager.ShowCards();
                currentExp = 0;
            }
        }
    }
}
