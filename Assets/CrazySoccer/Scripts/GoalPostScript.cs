using UnityEngine;

public class GoalPostScript : MonoBehaviour
{
    [SerializeField] private short goalTeamID;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("SoccerBall"))
        {
            Debug.Log($"Goal In!!!");

            short scoredTeam = (goalTeamID == 1) ? (short)2 : (short)1;
            GameManager.Instance.SendGoalEvent(scoredTeam);
            GameManager.Instance.ResetMatch();
        }
    }
}
