using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private Transform soccerBallTransform;
    Dictionary<ushort, PlayerObject> playerObjects = new Dictionary<ushort, PlayerObject>();

    private void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        ServerManager.Instance.packetHandlers.Add(PacketType.MoveInput, MovePacketHandler);
    }

    public void MovePacketHandler(BinaryReader br)
    {
        ushort playerID = br.ReadUInt16();
        PlayerObject playerObject = playerObjects[playerID];
        playerObject.currentHorizontalInput = br.ReadSingle();
        if (br.ReadBoolean()) playerObject.currentJumpInput = true;
    }

    public void SendGoalEvent(short scoredTeam)
    {
        GoalEventPacket goalPacket = new GoalEventPacket();
        goalPacket.ScoredTeam = scoredTeam;
        byte[] packet = goalPacket.Serialize();

        foreach (KeyValuePair<ulong, PlayerSession> session in ServerManager.Instance.playerSessions)
        {
            NetworkStream stream = session.Value.Client.GetStream();
            stream.Write(packet, 0, packet.Length);
        }
    }

    void FixedUpdate()
    {
        foreach (KeyValuePair<ushort, PlayerObject> item in playerObjects)
        {
            SyncPacket sync = new SyncPacket();
            sync.PlayerX = item.Value.transform.position.x;
            sync.PlayerY = item.Value.transform.position.y;
            sync.BallX = soccerBallTransform.position.x;
            sync.BallY = soccerBallTransform.position.y;
            byte[] syncBytes = sync.Serialize();

            foreach (KeyValuePair<ulong, PlayerSession> session in ServerManager.Instance.playerSessions)
            {
                NetworkStream stream = session.Value.Client.GetStream();
                stream.Write(syncBytes, 0, syncBytes.Length);
            }
        }
    }
}
