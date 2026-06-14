using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

public class ServerManager : MonoBehaviour
{
    static public ServerManager Instance;

    private TcpListener tcpListener;
    private ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    public Dictionary<ulong, PlayerSession> playerSessions { get; private set; } = new Dictionary<ulong, PlayerSession>();
    public Dictionary<PacketType, Action<BinaryReader>> packetHandlers = new Dictionary<PacketType, Action<BinaryReader>>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        tcpListener = new TcpListener(IPAddress.Any, NetworkConfig.ServerPort);
        tcpListener.Start();
        Debug.Log("서버 구동시작..");
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void OnAcceptClient(IAsyncResult ar)
    {
        ulong sessionID = GenerateSessionID();
        TcpClient client = tcpListener.EndAcceptTcpClient(ar);
        PlayerSession playerSession = new PlayerSession { Client = client, SessionID = sessionID };
        playerSessions.Add(sessionID, playerSession);

        mainThreadQueue.Enqueue(() =>
        {
            Debug.Log($"SessionID : {sessionID} is Connected");
        });

        ReceiveLoop(playerSession);
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void ReceiveLoop(PlayerSession playerSession)
    {
        NetworkStream stream = playerSession.Client.GetStream();
        byte[] headerBuffer = new byte[NetworkConfig.HeaderSize];

        stream.BeginRead(headerBuffer, 0, headerBuffer.Length, OnReadHeader, new object[] { stream, headerBuffer, playerSession });
    }

    private void OnReadHeader(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        NetworkStream stream = (NetworkStream)state[0];
        byte[] headerBuffer = (byte[])state[1];
        PlayerSession playerSession = (PlayerSession)state[2];

        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) { CloseClient(playerSession); return; }

            short packetSize = BitConverter.ToInt16(headerBuffer, 0);
            PacketType packetType = (PacketType)BitConverter.ToInt16(headerBuffer, 2);

            byte[] bodyBuffer = new byte[packetSize - NetworkConfig.HeaderSize];
            stream.BeginRead(bodyBuffer, 0, bodyBuffer.Length, OnReadBody, new object[] { stream, bodyBuffer, packetType, playerSession });
        }
        catch { CloseClient(playerSession); }
    }

    private void OnReadBody(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        NetworkStream stream = (NetworkStream)state[0];
        byte[] bodyBuffer = (byte[])state[1];
        PacketType packetType = (PacketType)state[2];
        PlayerSession playerSession = (PlayerSession)state[3];

        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) { CloseClient(playerSession); return; }

            using (MemoryStream ms = new MemoryStream(bodyBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                if (packetHandlers.TryGetValue(packetType, out var handler))
                {
                    handler.Invoke(br);
                }
                else
                {
                    Debug.LogError($"{packetType}은 등록되지 않음");
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
        ulong sessionID = playerSession.SessionID;
        playerSession.Client.Close();
        playerSessions.Remove(sessionID);
        Debug.Log($"{sessionID} is Disconnected");
    }

    void OnApplicationQuit()
    {
        Debug.Log("Server Closed");
        tcpListener?.Stop();
    }
}
