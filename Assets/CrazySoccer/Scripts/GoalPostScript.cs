using UnityEngine;

public class GoalPostScript : MonoBehaviour
{
    [SerializeField] private short goalTeamID;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("SoccerBall"))
        {
            // ★ 1. 게임 대기 상태(isMatchRunning == false)일 때의 처리
            // 점수를 올리거나 세리머니를 하지 않고, 오직 공만 중앙으로 조용히 돌려보냅니다.
            if (!GameManager.Instance.isMatchRunning)
            {
                Rigidbody2D ballRb = collision.GetComponent<Rigidbody2D>();
                if (ballRb != null)
                {
                    ballRb.linearVelocity = Vector2.zero;
                    ballRb.angularVelocity = 0f;
                    collision.transform.position = Vector2.zero;
                }
                return; // 여기서 함수를 종료하여 아래의 진짜 골 이벤트가 안 터지게 막습니다.
            }

            Debug.Log($"Goal In!!!");

            short scoredTeam = (goalTeamID == 1) ? (short)2 : (short)1;
            
            // 진짜 게임 중일 때는 골 이벤트를 발생시킵니다! (5초 세리머니 시작)
            GameManager.Instance.SendGoalEvent(scoredTeam);
            
            // ★ 2. 삭제된 부분: 즉시 리셋 범인!
            // GameManager.Instance.ResetMatch(); 
            // (이제 서버의 SendGoalEvent가 5초를 센 뒤에 알아서 ResetMatch를 호출해 줍니다.)
        }
    }
}