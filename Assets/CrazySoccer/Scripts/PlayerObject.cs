using System.IO;
using UnityEngine;

public class PlayerObject : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
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
            playerRigidbody.AddForce(Vector2.up * 7f, ForceMode2D.Impulse);
            currentJumpInput = false;
        }
    }
}
