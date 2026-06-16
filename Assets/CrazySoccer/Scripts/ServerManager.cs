using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using UnityEngine;

public class ServerManager : MonoBehaviour
{
    static public ServerManager Instance;

    private TcpListener tcpListener;
    public ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    public ConcurrentDictionary<ulong, PlayerSession> playerSessions { get; private set; } = new ConcurrentDictionary<ulong, PlayerSession>();
    public Dictionary<PacketType, Action<PlayerSession, BinaryReader>> packetHandlers = new Dictionary<PacketType, Action<PlayerSession, BinaryReader>>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        packetHandlers.Add(PacketType.MoveInput, GameManager.Instance.MovePacketHandler);

        tcpListener = new TcpListener(IPAddress.Any, NetworkConfig.ServerPort);
        tcpListener.Start();
        Debug.Log("Server Started");
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void OnAcceptClient(IAsyncResult ar)
    {
        try
        {
            TcpClient client = tcpListener.EndAcceptTcpClient(ar);
            ulong sessionID = GenerateSessionID();

            PlayerSession playerSession = new PlayerSession
            {
                Client = client,
                Stream = client.GetStream(),
                SessionID = sessionID
            };

            playerSessions.TryAdd(sessionID, playerSession);

            mainThreadQueue.Enqueue(() =>
            {
                GameManager.Instance.ConnectPlayer(playerSession);
            });

            ReceiveLoop(playerSession);
            tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
        }
        catch (Exception ex) { Debug.LogError($"Error: {ex.Message}"); }
    }

    private void ReceiveLoop(PlayerSession playerSession)
    {
        if (playerSession.Client == null || !playerSession.Client.Connected) return;

        playerSession.Stream.BeginRead(
            playerSession.HeaderBuffer, 0, playerSession.HeaderBuffer.Length,
            OnReadHeader, playerSession
        );
    }

    private void OnReadHeader(IAsyncResult ar)
    {
        PlayerSession playerSession = (PlayerSession)ar.AsyncState;

        try
        {
            int bytesRead = playerSession.Stream.EndRead(ar);
            if (bytesRead == 0) { CloseClient(playerSession); return; }
            int totalRead = bytesRead;
            while (totalRead < NetworkConfig.HeaderSize)
            {
                int read = playerSession.Stream.Read(playerSession.HeaderBuffer, totalRead, NetworkConfig.HeaderSize - totalRead);
                if (read == 0) { CloseClient(playerSession); return; }
                totalRead += read;
            }

            short packetSize = BitConverter.ToInt16(playerSession.HeaderBuffer, 0);
            PacketType packetType = (PacketType)BitConverter.ToInt16(playerSession.HeaderBuffer, 2);

            playerSession.BodyBuffer = new byte[packetSize - NetworkConfig.HeaderSize];
            
            playerSession.Stream.BeginRead(
                playerSession.BodyBuffer, 0, playerSession.BodyBuffer.Length, 
                OnReadBody, new object[] { packetType, playerSession } 
            );
        }
        catch { CloseClient(playerSession); }
    }

    private void OnReadBody(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        PacketType packetType = (PacketType)state[0];
        PlayerSession playerSession = (PlayerSession)state[1];

        try
        {
            int bytesRead = playerSession.Stream.EndRead(ar);
            if (bytesRead == 0) { CloseClient(playerSession); return; }

            int totalRead = bytesRead;
            while (totalRead < playerSession.BodyBuffer.Length)
            {
                int read = playerSession.Stream.Read(playerSession.BodyBuffer, totalRead, playerSession.BodyBuffer.Length - totalRead);
                if (read == 0) { CloseClient(playerSession); return; }
                totalRead += read;
            }

            using (MemoryStream ms = new MemoryStream(playerSession.BodyBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                if (packetHandlers.TryGetValue(packetType, out var handler))
                {
                    handler.Invoke(playerSession, br);
                }
                else
                {
                    Debug.LogError($"[서버] {packetType}은 등록되지 않은 패킷입니다.");
                }
            }

            ReceiveLoop(playerSession);
        }
        catch { CloseClient(playerSession); }
    }

    private ulong GenerateSessionID()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        return BitConverter.ToUInt64(buffer);
    }

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    private void CloseClient(PlayerSession playerSession)
    {
        playerSession.Client.Close();
        playerSessions.TryRemove(playerSession.SessionID, out _);
        Debug.Log($"Player{playerSession.PlayerID} Disconnectd.");
    }

    void OnApplicationQuit()
    {
        Debug.Log("Server Closed");
        tcpListener?.Stop();
    }
}
