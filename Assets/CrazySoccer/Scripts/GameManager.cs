using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

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
    }
}
