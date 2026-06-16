using System.IO;
using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
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
}