using UnityEngine;

[CreateAssetMenu(fileName = "CardUpgrade", menuName = "Cards/Upgrade")]
public class Card : ScriptableObject
{
    public string itemName;
    public UpgradeType type;
    public Sprite icon;
    [TextArea] public string description;
    public GameObject prefb;
    public int value; //how much to increase or level up
    public int maxLvl;
    public enum UpgradeType { WEAPON, ARMOR, ATTACKSPEED,HEALTHBOOST }

    public void UpgradePlayerStates(Player player)
    {
        //Upgrade player stats when player done
        switch(type)
        {
            case UpgradeType.ARMOR:
                player.PlusDmgReduction(value);
                break;
            case UpgradeType.WEAPON:
                player.NewWeapon(itemName);
                break;
            case UpgradeType.HEALTHBOOST:
                
                break;
            case UpgradeType.ATTACKSPEED:

                break;

        }
    }

    

}
