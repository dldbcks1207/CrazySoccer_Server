using System.IO;
using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    [Header("Ball Force")]
    [SerializeField] private LayerMask ballLayer;
    [SerializeField] private Transform kickPoint;
    [SerializeField] private float kickRadius = 0.37f;
    [SerializeField] private float dribbleForce = 4f;
    [SerializeField] private float kickForce = 6f;
    [SerializeField] private float kickY = 0.1f;

    [SerializeField] private LayerMask groundLayer;

    [Header("점프 선입력 (Jump Buffering)")]
    [SerializeField] private float jumpBufferTime = 0.2f;
    private float jumpBufferCounter;

    public float currentHorizontalInput = 0;
    public bool currentJumpInput = false;

    [SerializeField] private Rigidbody2D playerRigidbody;

    void FixedUpdate()
    {
        if (playerRigidbody == null) return;

        float targetVelocityX = currentHorizontalInput * moveSpeed;
        playerRigidbody.linearVelocity = new Vector2(targetVelocityX, playerRigidbody.linearVelocity.y);

        if (currentJumpInput)
        {
            jumpBufferCounter = jumpBufferTime;
            currentJumpInput = false;
        }
        else
        {
            jumpBufferCounter -= Time.fixedDeltaTime;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.75f, groundLayer);
        if (jumpBufferCounter > 0f && hit.collider != null)
        {
            playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, 0f);
            playerRigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpBufferCounter = 0f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Dribble
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

    public void TryKick()
    {
        Debug.Log("Kick");
        Vector2 origin = (Vector2)kickPoint.position;
        RaycastHit2D hit = Physics2D.CircleCast(origin, kickRadius, Vector2.zero, 0f, ballLayer);

        if (hit.collider != null)
        {
            ServerBall ball = hit.collider.GetComponent<ServerBall>();
            if (ball != null)
            {
                Vector2 kickDir = (hit.collider.transform.position - transform.position).normalized;
                kickDir.y += kickY;
                ball.ReceiveForce(kickDir.normalized, kickForce);
            }
        }
    }
}