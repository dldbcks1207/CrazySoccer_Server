using System.IO;
using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    [Header("Ball Force")]
    [SerializeField] private LayerMask ballLayer;
    [SerializeField] private LayerMask playerLayer;

    [SerializeField] private Transform kickPoint;
    [SerializeField] private float kickRadius = 0.37f;
    [SerializeField] private float dribbleForce = 4f;
    [SerializeField] private float kickForce = 6f;
    [SerializeField] private float kickY = 0.1f;

    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Buffering")]
    [SerializeField] private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter;

    [Header("Movement Setting")]
    [SerializeField] private float normalGrip = 15f;
    [SerializeField] private float knockbackGrip = 1f;

    private bool isKnockedBack = false;
    private float knockbackTimer = 0f;

    public float currentHorizontalInput = 0;
    public bool currentJumpInput = false;

    [SerializeField] private Rigidbody2D playerRigidbody;

    void FixedUpdate()
    {
        if (playerRigidbody == null) return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.75f, groundLayer);
        bool isGrounded = hit.collider != null;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
        }
        else if (isGrounded)
        {
            isKnockedBack = false;
        }

        float activeGrip = isKnockedBack ? knockbackGrip : normalGrip;

        float targetVelocityX = currentHorizontalInput * moveSpeed;
        float smoothVelocityX = Mathf.Lerp(playerRigidbody.linearVelocity.x, targetVelocityX, Time.fixedDeltaTime * activeGrip);

        playerRigidbody.linearVelocity = new Vector2(smoothVelocityX, playerRigidbody.linearVelocity.y);

        if (currentJumpInput)
        {
            jumpBufferCounter = jumpBufferTime;
            currentJumpInput = false;
        }
        else
        {
            jumpBufferCounter -= Time.fixedDeltaTime;
        }

        if (jumpBufferCounter > 0f && isGrounded)
        {
            playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, 0f);
            playerRigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpBufferCounter = 0f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("SoccerBall"))
        {
            ServerBall ball = collision.gameObject.GetComponent<ServerBall>();
            if (ball != null && Mathf.Abs(currentHorizontalInput) > 0.1f)
            {
                Vector2 kickDir = (ball.transform.position - transform.position).normalized;
                kickDir.y = 0f;
                ball.ReceiveForce(kickDir.normalized, dribbleForce);
            }
        }
    }

    public void TryKick(byte force, bool isDriven, bool directionIsLeft)
    {
        float kickOffsetX = Mathf.Abs(kickPoint.localPosition.x);
        float sign = directionIsLeft ? -1f : 1f;

        kickPoint.localPosition = new Vector3(kickOffsetX * sign, kickPoint.localPosition.y, kickPoint.localPosition.z);

        Vector2 origin = (Vector2)kickPoint.position;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, kickRadius, Vector2.zero, 0f, ballLayer | playerLayer);

        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject)
            {
                if (hit.collider.TryGetComponent(out ServerBall ball))
                {
                    Vector2 kickDir = (hit.collider.transform.position - transform.position).normalized;
                    kickDir.x = Mathf.Abs(kickDir.x) * sign;

                    if (isDriven)
                        kickDir.y = 0;
                    else
                        kickDir.y += kickY;

                    ball.ReceiveForce(kickDir.normalized, force / 100f * kickForce);
                }

                if (hit.collider.TryGetComponent(out PlayerObject otherPlayer))
                {
                    Vector2 kickDir = (hit.collider.transform.position - transform.position).normalized;
                    kickDir.x = Mathf.Abs(kickDir.x) * sign;
                    kickDir.y += kickY;

                    otherPlayer.ReceiveForce(kickDir.normalized, force / 100f * (kickForce * 70f));
                }
            }
        }
    }

    public void ReceiveForce(Vector2 force, float forceMagnitude)
    {
        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.AddForce(force * forceMagnitude, ForceMode2D.Impulse);

        isKnockedBack = true;
        knockbackTimer = 0.2f;
    }
}