using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private Transform soccerBallTransform;
    [SerializeField] private PlayerObject playerPrefab;
    ConcurrentDictionary<ushort, PlayerObject> playerObjects = new ConcurrentDictionary<ushort, PlayerObject>();
    private ushort playerIDCursor = 1;

    private void Awake()
    {
        Instance = this;
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
                // 1P면 왼쪽(-5), 2P면 오른쪽(5)
                item.Value.transform.position = (item.Key == 1) ? new Vector2(-5f, 0f) : new Vector2(5f, 0f);
            }
        }
        Debug.Log("경기장 중앙으로 모두 리셋 완료!");
    }

    public void MovePacketHandler(PlayerSession session, BinaryReader br)
    {
        if (playerObjects.TryGetValue(session.PlayerID, out PlayerObject playerObject))
        {
            playerObject.currentHorizontalInput = br.ReadSingle();
            if (br.ReadBoolean()) playerObject.currentJumpInput = true;
        }
    }

    void FixedUpdate()
    {
        foreach (var item in playerObjects)
        {
            PlayerSyncPacket sync = new PlayerSyncPacket
            {
                PlayerID = item.Key,
                PlayerX = item.Value.transform.position.x,
                PlayerY = item.Value.transform.position.y
            };
            byte[] syncBytes = sync.Serialize();

            foreach (var session in ServerManager.Instance.playerSessions.Values)
            {
                session.Stream.Write(syncBytes, 0, syncBytes.Length);
            }
        }

        BallSyncPacket ballSync = new BallSyncPacket();
        ballSync.BallX = soccerBallTransform.position.x;
        ballSync.BallY = soccerBallTransform.position.y;
        byte[] ballBytes = ballSync.Serialize();

        foreach (var session in ServerManager.Instance.playerSessions.Values)
        {
            session.Stream.Write(ballBytes, 0, ballBytes.Length);
        }
    }
}
