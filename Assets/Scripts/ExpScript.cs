using UnityEngine;

public class ExpScript : MonoBehaviour
{
    [SerializeField] public int exp = 5;
    [SerializeField] Transform player;
    [SerializeField] Rigidbody2D rb;
     void Start()
    {
        player = GameObject.FindWithTag("Player").transform;
        rb = GetComponent<Rigidbody2D>();
    }
    void FixedUpdate()
    {
        if((this.transform.position - player.position).magnitude < 1)
        {
            rb.AddRelativeForce(player.position.normalized * 1.5f,ForceMode2D.Impulse);
        }
    }
    public void Comsumed()
    {
        Destroy(this.gameObject);
    }
}
