using UnityEngine;
using System.Collections.Generic;

public class CardSelectionManager : MonoBehaviour
{
    public List<Card> allCards;
    public Transform cardParent;
    public GameObject[] cardUIPrefab;
    public GameObject cardUI;
    [SerializeField] private CanvasGroup cardUICanvasGroup; // optional fade/visibility without disabling children
    [SerializeField] private bool useExistingChildren = true; // Use Card1/Card2/Card3 under panel instead of instantiating
    public Player player;

    public GameManager gm;

    public void ShowCards()
    {
        // Pick random 3 cards
        List<Card> choices = new List<Card>(allCards);
        for (int i = 0; i < 3 && choices.Count > 0; i++)
        {
            int rand = Random.Range(0, choices.Count);
            Card card = choices[rand];
            choices.RemoveAt(rand);

            if (useExistingChildren && cardParent != null && cardParent.childCount >= i + 1)
            {
                Transform slot = cardParent.GetChild(i);
                slot.gameObject.SetActive(true);
                var cui = slot.GetComponent<CardUI>();
                if (cui != null)
                {
                    cui.Setup(card, this);
                }
            }
            else
            {
                // Instantiate from prefab if provided
                if (cardUIPrefab != null && i < cardUIPrefab.Length && cardUIPrefab[i] != null)
                {
                    GameObject go = Instantiate(cardUIPrefab[i], cardParent);
                    var cui = go.GetComponent<CardUI>();
                    if (cui != null) cui.Setup(card, this);
                }
            }
        }
        EnableCardUI();
        gm.TogglePause();
    }

    public void ChosenCard(Card card)
    {
        card.UpgradePlayerStates(player);
        gm.TogglePause();
        DisableCardUI();
    }

    private void EnsureCanvasGroup()
    {
        if (cardUICanvasGroup == null && cardUI != null)
        {
            cardUICanvasGroup = cardUI.GetComponent<CanvasGroup>();
            if (cardUICanvasGroup == null)
            {
                cardUICanvasGroup = cardUI.AddComponent<CanvasGroup>();
                cardUICanvasGroup.alpha = 0f;
                cardUICanvasGroup.interactable = false;
                cardUICanvasGroup.blocksRaycasts = false;
            }
        }
    }

    public void EnableCardUI()
    {
        EnsureCanvasGroup();
        if (cardUICanvasGroup != null)
        {
            cardUI.SetActive(true);
            cardUICanvasGroup.alpha = 1f;
            cardUICanvasGroup.interactable = true;
            cardUICanvasGroup.blocksRaycasts = true;
        }
        else
        {
            cardUI.SetActive(true);
        }
    }

    public void DisableCardUI()
    {
        EnsureCanvasGroup();
        if (cardUICanvasGroup != null)
        {
            // Hide without disabling children/components
            cardUICanvasGroup.alpha = 0f;
            cardUICanvasGroup.interactable = false;
            cardUICanvasGroup.blocksRaycasts = false;
        }
        else
        {
            cardUI.SetActive(false);
        }
    }
   
}
