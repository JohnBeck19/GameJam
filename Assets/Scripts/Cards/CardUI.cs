using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class CardUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [SerializeField] private Card card;
    private CardSelectionManager manager;


    //private void Start()
    //{
    //    iconImage.sprite = card.icon;

    //    nameText.text = card.itemName;
    //    descriptionText.text = card.description;
    //}

    public void Setup(Card card, CardSelectionManager mgr)
    {
        this.card = card;
       
        manager = mgr;

        iconImage.sprite = card.icon;
        nameText.text = card.itemName;
        descriptionText.text = card.description;
    }


    public void ChosenCard()
    {
        manager.ChosenCard(card);
    }

}
