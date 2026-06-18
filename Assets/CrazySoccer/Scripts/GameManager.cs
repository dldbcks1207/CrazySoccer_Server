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
        if (isMatchRunning && !isGamePaused)
        {
            matchTimer -= Time.deltaTime;

            if (matchTimer <= 0f)
            {
                matchTimer = 0f;
                isMatchRunning = false;

                // ★ 기존의 SetGamePause(true); 를 지우고 새로운 엔딩 시퀀스 호출!
                EndGameSequence();
            }
        }
    }

    private async void EndGameSequence()
    {
        Debug.Log("=================================");
        Debug.Log("            게임종료             ");
        Debug.Log("=================================");

        // 1. 골대 이벤트처럼 공만 허공에 꽁꽁 얼려버립니다. (유저들은 자유롭게 이동 가능)
        Rigidbody2D ballRb = soccerBallTransform.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
            ballRb.simulated = false;
        }

        // 2. 5초간 대기 (마지막 세리머니 타임!)
        await Task.Delay(5000);

        // 3. 클라이언트들에게 "집에 가라!" (타이틀로 이동) 패킷 발송
        GameEndPacket gameEndPacket = new GameEndPacket();
        byte[] packet = gameEndPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(packet, 0, packet.Length);
        }

        // 4. 패킷이 무사히 날아갈 시간을 1초 줍니다.
        await Task.Delay(1000);

        // 5. 서버 방 청소 (새로운 매칭을 받을 준비)
        ServerManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            // 플레이어 오브젝트 다 파괴하고 딕셔너리 비우기
            foreach (var item in playerObjects.Values)
            {
                Destroy(item.gameObject);
            }
            playerObjects.Clear();
            playerIDCursor = 1; // ID 발급기 초기화

            // 공 원래대로 녹여놓기
            if (ballRb != null)
            {
                ballRb.simulated = true;
                soccerBallTransform.position = Vector2.zero;
            }

            Debug.Log("[서버] 방 청소 완료! 새로운 매칭을 기다립니다.");
        });
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

        // ★ 수정: 2명이 꽉 찼을 때 즉시 멈추지 않고 몸풀기 시퀀스를 호출합니다!
        if (playerIDCursor == 3)
        {
            StartWarmUpSequence();
        }
    }

    // ★ 추가: 5초간 몸을 풀게 놔두고, 알아서 시작 시퀀스로 넘어가는 마법의 함수
    private async void StartWarmUpSequence()
    {
        Debug.Log("[서버] 2명 접속 완료! 5초간 몸풀기 시간 시작.");

        // 1. 5초간 대기 (이 동안 유저들은 자유롭게 움직이고 공을 찰 수 있습니다!)
        // (isMatchRunning이 아직 false이기 때문에 타이머도 3분에 멈춰있고, 골을 넣어도 점수가 오르지 않습니다)
        await Task.Delay(5000);

        Debug.Log("[서버] 몸풀기 끝! 진짜 매치 시작 시퀀스 돌입.");

        // 2. 5초 뒤, 메인 스레드에서 기존의 '화면 가림 -> 리셋 -> 시작' 콤보를 실행합니다.
        ServerManager.Instance.mainThreadQueue.Enqueue(() =>
        {
            // 일단 애들 움직임을 멈춰!
            SetGamePause(true);

            // 화면 가려!
            SendGameWait(() =>
            {
                ServerManager.Instance.mainThreadQueue.Enqueue(() =>
                {
                    // 화면 가려진 틈을 타서 각자 진영으로 위치 리셋
                    ResetMatch();

                    // 다시 화면 열어!
                    SendGameStart(() =>
                    {
                        ServerManager.Instance.mainThreadQueue.Enqueue(() =>
                        {
                            // 움직임 봉인 해제 & 진짜 게임 시작 (타이머 굴러감)
                            SetGamePause(false);
                            matchTimer = 180f;
                            isMatchRunning = true;
                            Debug.Log("게임 시작! 조작 잠금 해제 및 타이머 시작됨.");
                        });
                    });
                });
            });
        });
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

    public void AnimationPacketHandler(PlayerSession session, BinaryReader br)
    {
        byte animNum = br.ReadByte();

        // 방송용 패킷 조립
        ReturnAnimationPacket returnPacket = new ReturnAnimationPacket();
        returnPacket.playerID = session.PlayerID;
        returnPacket.animNum = animNum;

        byte[] packetBytes = returnPacket.Serialize();

        // 방 안에 있는 모든 클라이언트에게 발송
        foreach (var s in ServerManager.Instance.playerSessions.Values)
        {
            s.Stream.Write(packetBytes, 0, packetBytes.Length);
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