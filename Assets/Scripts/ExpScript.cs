using UnityEngine;

public class ExpScript : MonoBehaviour
{
    [SerializeField] public int exp = 5;
    [SerializeField] Transform player;
    [SerializeField] Rigidbody2D rb;
    [SerializeField] private float magnetRange = 3f;
    [SerializeField] private float magnetSpeed = 6f;
    [SerializeField] private bool useTriggerPickup = true;

    void Start()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        rb = GetComponent<Rigidbody2D>();
        var col = GetComponent<Collider2D>();
        if (useTriggerPickup && col != null) col.isTrigger = true;
    }
    void FixedUpdate()
    {
        if (player == null) return;
        Vector2 toPlayer = (player.position - transform.position);
        float dist = toPlayer.magnitude;
        if (dist <= magnetRange)
        {
            Vector2 dir = toPlayer.normalized;
            if (rb != null)
            {
                rb.linearVelocity = dir * magnetSpeed;
            }
            else
            {
                transform.position += (Vector3)(dir * magnetSpeed * Time.fixedDeltaTime);
            }
        }
    }
    public void Comsumed()
    {
        Destroy(this.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTriggerPickup) return;
        if (other == null) return;
        if (!other.CompareTag("Player")) return;
        var lvl = other.GetComponent<LevelScrpit>();
        if (lvl == null)
        {
            lvl = other.GetComponentInParent<LevelScrpit>();
        }
        if (lvl != null)
        {
            lvl.AddExp(exp);
            Comsumed();
        }
    }
}
