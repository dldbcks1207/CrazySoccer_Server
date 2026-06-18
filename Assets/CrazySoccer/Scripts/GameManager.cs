using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private Transform soccerBallTransform;
    [SerializeField] private PlayerObject playerPrefab;
    ConcurrentDictionary<ushort, PlayerObject> playerObjects = new ConcurrentDictionary<ushort, PlayerObject>();
    private ushort playerIDCursor = 1;

    public bool isGamePaused { get; private set; } = false;

    // ★ 추가: 게임 진행 시간 관련 변수 (3분 = 180초)
    public float matchTimer = 180f;
    public bool isMatchRunning = false;

    private void Awake()
    {
        Instance = this;
    }

    // ★ 추가: 매 프레임마다 시간을 체크하는 Update 함수
    private void Update()
    {
        // 게임이 진행 중이고, 일시정지 상태가 아닐 때만 시간이 흘러갑니다.
        if (isMatchRunning && !isGamePaused)
        {
            matchTimer -= Time.deltaTime;

            if (matchTimer <= 0f)
            {
                matchTimer = 0f;
                isMatchRunning = false; // 타이머 완전 종료

                // ★ 경기 종료! (모든 선수와 공을 그 자리에 얼려버립니다)
                SetGamePause(true);
                Debug.Log("=================================");
                Debug.Log("            게임종료             ");
                Debug.Log("=================================");

                // 나중에 여기에 '클라이언트들에게 게임 종료 화면 띄우라는 패킷'을 쏘시면 됩니다!
            }
        }
    }

    public void SetGamePause(bool pause)
    {
        isGamePaused = pause;

        Rigidbody2D ballRb = soccerBallTransform.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.simulated = !pause;
        }

        foreach (var item in playerObjects.Values)
        {
            Rigidbody2D pRb = item.GetComponent<Rigidbody2D>();
            if (pRb != null)
            {
                pRb.simulated = !pause;
            }

            if (pause)
            {
                item.currentHorizontalInput = 0f;
                item.currentJumpInput = false;
            }
        }

        Debug.Log($"[서버] 게임 물리 및 입력 상태 변경. (Paused: {pause})");
    }

    public void ConnectPlayer(PlayerSession playerSession)
    {
        ushort playerID = playerIDCursor++;
        playerSession.PlayerID = playerID;

        PlayerObject playerObj = Instantiate(playerPrefab);
        playerObjects.TryAdd(playerID, playerObj);
        Debug.Log($"Player{playerSession.PlayerID}({playerSession.SessionID}) is Connected");

        SendSessionPacket sessionPacket = new SendSessionPacket
        {
            SessionID = playerSession.SessionID,
            PlayerID = playerSession.PlayerID,
            PlayerNum = (ushort)playerObjects.Count
        };
        byte[] packet = sessionPacket.Serialize();
        playerSession.Stream.Write(packet, 0, packet.Length);

        SendNewPlayerPacket(playerID);

        if (playerIDCursor == 3)
        {
            SetGamePause(true);

            SendGameWait(() =>
            {
                ServerManager.Instance.mainThreadQueue.Enqueue(() =>
                {
                    foreach (KeyValuePair<ushort, PlayerObject> item in playerObjects)
                    {
                        Rigidbody2D pRb = item.Value.GetComponent<Rigidbody2D>();
                        if (pRb != null)
                        {
                            pRb.linearVelocity = Vector2.zero;
                            item.Value.transform.position = (item.Key == 1) ? new Vector2(-18f, 0f) : new Vector2(18f, 0f);
                        }
                    }

                    SendGameStart(() =>
                    {
                        ServerManager.Instance.mainThreadQueue.Enqueue(() =>
                        {
                            SetGamePause(false);

                            // ★ 추가: 드디어 진짜 게임 시작! 타이머가 굴러가도록 스위치를 켭니다.
                            matchTimer = 180f;
                            isMatchRunning = true;

                            Debug.Log("게임 시작! 조작 잠금 해제 및 타이머 시작됨.");
                        });
                    });
                });
            });
        }
    }

    public async void SendGameWait(Action afterEvent = null)
    {
        GameWaitPacket gameWaitPacket = new GameWaitPacket();
        byte[] pakcet = gameWaitPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(pakcet, 0, pakcet.Length);
        }

        if (afterEvent != null)
        {
            await Task.Delay(1500);
            afterEvent.Invoke();
        }
    }

    public async void SendGameStart(Action afterEvent = null)
    {
        GameStartPacket gameStartPacket = new GameStartPacket();
        byte[] pakcet = gameStartPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(pakcet, 0, pakcet.Length);
        }

        if (afterEvent != null)
        {
            await Task.Delay(500);
            afterEvent.Invoke();
        }
    }

    public void SendNewPlayerPacket(ushort playerID)
    {
        NewSessionConnectPacket newSessionPacket = new NewSessionConnectPacket { PlayerID = playerID };
        byte[] packet = newSessionPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            if (session.PlayerID == playerID) continue;
            session.Stream.Write(packet, 0, packet.Length);
        }
    }

    public async void SendGoalEvent(short scoredTeam)
    {
        // 1. 클라이언트들에게 골이 들어갔다고 알림 (점수판 업데이트용)
        GoalEventPacket goalEventPacket = new GoalEventPacket();
        goalEventPacket.ScoredTeam = scoredTeam;
        byte[] goalPacket = goalEventPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(goalPacket, 0, goalPacket.Length);
        }

        Debug.Log($"[서버] {scoredTeam}팀 골! 5초간 세레머니 타임 시작.");

        // 2. 공만 그 자리에 얼려버림 (유저들은 자유롭게 이동 가능)
        Rigidbody2D ballRb = soccerBallTransform.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
            ballRb.simulated = false; // 공의 물리 연산 완전 정지!
        }

        // 3. 5초 대기 (세레머니 타임)
        await Task.Delay(5000);

        // 4. 5초 뒤 화면을 까맣게 가림 (GameWait)
        SendGameWait(() =>
        {
            ServerManager.Instance.mainThreadQueue.Enqueue(() =>
            {
                // 5. 화면이 가려진 틈을 타서 공과 유저 위치를 중앙으로 리셋!
                ResetMatch();

                // 6. 위치 세팅이 끝났으니 화면을 다시 엶 (GameStart)
                SendGameStart();
            });
        });
    }

    public void ResetMatch()
    {
        Rigidbody2D ballRb = soccerBallTransform.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.simulated = true; // ★ 얼려놨던 공을 다시 녹여줌!
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
            soccerBallTransform.position = Vector2.zero;
        }

        foreach (KeyValuePair<ushort, PlayerObject> item in playerObjects)
        {
            Rigidbody2D pRb = item.Value.GetComponent<Rigidbody2D>();
            if (pRb != null)
            {
                pRb.linearVelocity = Vector2.zero;
                item.Value.transform.position = (item.Key == 1) ? new Vector2(-18f, 0f) : new Vector2(18f, 0f);
            }
        }
        Debug.Log("경기장 중앙으로 모두 리셋 완료!");
    }

    public void MovePacketHandler(PlayerSession session, BinaryReader br)
    {
        if (isGamePaused) return;

        if (playerObjects.TryGetValue(session.PlayerID, out PlayerObject playerObject))
        {
            playerObject.currentHorizontalInput = br.ReadSingle();
            if (br.ReadBoolean()) playerObject.currentJumpInput = true;
        }
    }

    public void KickPacketHandler(PlayerSession session, BinaryReader br)
    {
        if (isGamePaused) return;

        byte force = br.ReadByte();
        bool isDriven = br.ReadBoolean();
        bool directionIsLeft = br.ReadBoolean();

        ServerManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            if (playerObjects.TryGetValue(session.PlayerID, out PlayerObject playerObject))
            {
                playerObject.TryKick(force, isDriven, directionIsLeft);
            }
        });
    }

    void FixedUpdate()
    {
        WorldSyncPacket syncPacket = new WorldSyncPacket();

        syncPacket.BallX = soccerBallTransform.position.x;
        syncPacket.BallY = soccerBallTransform.position.y;

        // ★ 추가: 서버의 현재 시간을 패킷에 탑재!
        syncPacket.MatchTimer = matchTimer;

        foreach (var item in playerObjects)
        {
            syncPacket.PlayerList.Add(new WorldSyncPacket.PlayerData
            {
                PlayerID = item.Key,
                PlayerX = item.Value.transform.position.x,
                PlayerY = item.Value.transform.position.y
            });
        }

        byte[] syncBytes = syncPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(syncBytes, 0, syncBytes.Length);
        }
    }
}