using UnityEngine;

public class ServerBall : MonoBehaviour
{
    private Rigidbody2D rb;

    [SerializeField] private float dribbleForce = 12f;
    [Header("축구공 물리 세팅")]
    [SerializeField] private float maxSpeed = 50f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponent<Rigidbody2D>() != null && collision.gameObject.name.Contains("Player"))
        {
            Vector2 dribbleDirection = (transform.position - collision.transform.position).normalized;

            dribbleDirection.y = 0f;
            dribbleDirection = dribbleDirection.normalized;

            rb.linearVelocity = Vector2.zero;

            rb.AddForce(dribbleDirection * dribbleForce, ForceMode2D.Impulse);
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
}
