using UnityEngine;

[CreateAssetMenu(fileName = "CardUpgrade", menuName = "Cards/Upgrade")]
public class Card : ScriptableObject
{
    public string itemName;
    public UpgradeType type;
    public Sprite icon;
    public int value; //how much to increase or level up
    public enum UpgradeType { WEAPON, ARMOR, ATTACKSPEED,HEALTHBOOST }

    public void UpgradePlayerStates()
    {
        //Upgrade player stats when player done
        switch(type)
        {
            case UpgradeType.ARMOR:
                Debug.Log("Ammor");
                break;
            case UpgradeType.WEAPON:
                Debug.Log("wepon");
                break;
        }
    }

}
