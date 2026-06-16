using UnityEngine;

public class ServerBall : MonoBehaviour
{
    private Rigidbody2D rb;
    [SerializeField] private float maxSpeed = 50f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void ReceiveForce(Vector2 force, float forceMagnitude)
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force * forceMagnitude, ForceMode2D.Impulse);
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