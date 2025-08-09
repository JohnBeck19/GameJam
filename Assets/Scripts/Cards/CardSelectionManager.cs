using UnityEngine;
using System.Collections.Generic;

public class CardSelectionManager : MonoBehaviour
{
    public List<Card> allCards;
    public Transform cardParent;
    public GameObject[] cardUIPrefab;

    public void ShowCards()
    {
        // Clear old cards
        foreach (Transform child in cardParent) Destroy(child.gameObject);

        // Pick random 3 cards
        List<Card> choices = new List<Card>(allCards);
        for (int i = 0; i < 3 && choices.Count > 0; i++)
        {
            int rand = Random.Range(0, choices.Count);
            Card card = choices[rand];
            choices.RemoveAt(rand);

            GameObject cardUI = Instantiate(cardUIPrefab[i], cardParent);
            cardUI.GetComponent<CardUI>().Setup(card, this);
        }
    }

    public void ChosenCard(Card card)
    {
        card.UpgradePlayerStates();
    }
   
}
