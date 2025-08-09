using UnityEngine;
using UnityEngine.EventSystems;

public class BtnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  
    [SerializeField] Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        animator.SetBool("Hover", true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        animator.SetBool("Hover", false);
    }
}
