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

    // ★ 추가: 게임 멈춤 상태를 관리하는 플래그
    public bool isGamePaused { get; private set; } = false;

    private void Awake()
    {
        Instance = this;
    }

    // ★ 추가: 딸깍! 한 번으로 게임을 멈췄다 풀었다 하는 마법의 함수
    public void SetGamePause(bool pause)
    {
        isGamePaused = pause;

        // 1. 공 얼리기 / 녹이기
        Rigidbody2D ballRb = soccerBallTransform.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
            ballRb.simulated = !pause; // false가 되면 물리 연산 정지, 공중 부양!
        }

        // 2. 모든 플레이어 얼리기 / 녹이기
        foreach (var item in playerObjects.Values)
        {
            Rigidbody2D pRb = item.GetComponent<Rigidbody2D>();
            if (pRb != null)
            {
                pRb.simulated = !pause;
            }

            // 정지 상태일 때 혹시 모를 미끄러짐을 방지하기 위해 입력값 강제 초기화
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
                        SetGamePause(false);
                    });
                });
                SetGamePause(true);
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
            await Task.Delay(1000);
            afterEvent.Invoke();
        }
    }

    public async void SendGameStart(Action afterEvent = null)
    {
        GameWaitPacket gameWaitPacket = new GameWaitPacket();
        byte[] pakcet = gameWaitPacket.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(pakcet, 0, pakcet.Length);
        }

        if (afterEvent != null)
        {
            await Task.Delay(1000);
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

    public void SendGoalEvent(short scoredTeam)
    {
        GoalEventPacket goalEventPacket = new GoalEventPacket();
        goalEventPacket.ScoredTeam = scoredTeam;

        byte[] goalPacket = goalEventPacket.Serialize();
        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(goalPacket, 0, goalPacket.Length);
        }
    }

    public void ResetMatch()
    {
        Rigidbody2D ballRb = soccerBallTransform.GetComponent<Rigidbody2D>();
        if (ballRb != null)
        {
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
        // ★ 추가: 게임이 정지 상태라면 인풋 패킷을 뜯어보지도 않고 무시합니다.
        if (isGamePaused) return;

        if (playerObjects.TryGetValue(session.PlayerID, out PlayerObject playerObject))
        {
            playerObject.currentHorizontalInput = br.ReadSingle();
            if (br.ReadBoolean()) playerObject.currentJumpInput = true;
        }
    }

    public void KickPacketHandler(PlayerSession session, BinaryReader br)
    {
        // ★ 추가: 게임이 정지 상태라면 킥 인풋도 무시합니다.
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