using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class ServerNetwork : MonoBehaviour
{
    private TcpListener tcpListener;
    private float currentHorizontalInput = 0f;
    private bool currentJumpInput = false;
    private ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
    private TcpClient activeClient;
    public Rigidbody2D serverPlayerRigidbody;
    public Transform soccerBallTransform;
    public float moveSpeed = 5f;
    private Dictionary<PacketType, Action<BinaryReader>> packetHandlers = new Dictionary<PacketType, Action<BinaryReader>>();
    void Start()
    {
        packetHandlers.Add(PacketType.MoveInput, MovePacketHandler);
        tcpListener = new TcpListener(IPAddress.Any, 9000);
        tcpListener.Start();
        Debug.Log("서버 권위형 하이브리드 대비 물리 서버 가동... 클라이언트를 기다립니다.");
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void OnAcceptClient(IAsyncResult ar)
    {
        TcpClient client = tcpListener.EndAcceptTcpClient(ar);
        activeClient = client;

        mainThreadQueue.Enqueue(() =>
        {
            Debug.Log("클라이언트 조종기가 서버에 안전하게 바인딩되었습니다!");
        });

        ReceiveLoop(client);
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void ReceiveLoop(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] headerBuffer = new byte[4];

        stream.BeginRead(headerBuffer, 0, headerBuffer.Length, OnReadHeader, new object[] { stream, headerBuffer, client });
    }

    private void OnReadHeader(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        NetworkStream stream = (NetworkStream)state[0];
        byte[] headerBuffer = (byte[])state[1];
        TcpClient client = (TcpClient)state[2];

        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) { CloseClient(client); return; }

            short packetSize = BitConverter.ToInt16(headerBuffer, 0);
            PacketType packetType = (PacketType)BitConverter.ToInt16(headerBuffer, 2);

            byte[] bodyBuffer = new byte[packetSize - 4];
            stream.BeginRead(bodyBuffer, 0, bodyBuffer.Length, OnReadBody, new object[] { stream, bodyBuffer, packetType, client });
        }
        catch { CloseClient(client); }
    }

    private void OnReadBody(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        NetworkStream stream = (NetworkStream)state[0];
        byte[] bodyBuffer = (byte[])state[1];
        PacketType packetType = (PacketType)state[2];
        TcpClient client = (TcpClient)state[3];

        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) { CloseClient(client); return; }

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

            ReceiveLoop(client);
        }
        catch { CloseClient(client); }
    }

    private void MovePacketHandler(BinaryReader br)
    {
        currentHorizontalInput = br.ReadSingle();
        if (br.ReadBoolean()) currentJumpInput = true;
    }

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    void FixedUpdate()
    {
        if (serverPlayerRigidbody == null) return;

        float targetVelocityX = currentHorizontalInput * moveSpeed;
        serverPlayerRigidbody.linearVelocity = new Vector2(targetVelocityX, serverPlayerRigidbody.linearVelocity.y);

        if (currentJumpInput)
        {
            serverPlayerRigidbody.AddForce(Vector2.up * 7f, ForceMode2D.Impulse);
            currentJumpInput = false;
        }

        if (activeClient != null && activeClient.Connected)
        {
            SyncPacket sync = new SyncPacket();
            sync.PlayerX = serverPlayerRigidbody.position.x;
            sync.PlayerY = serverPlayerRigidbody.position.y;
            sync.BallX = soccerBallTransform.position.x;
            sync.BallY = soccerBallTransform.position.y;
            byte[] syncBytes = sync.Serialize();
            NetworkStream stream = activeClient.GetStream();
            stream.Write(syncBytes, 0, syncBytes.Length);
        }
    }

    private void CloseClient(TcpClient client)
    {
        Debug.Log("클라이언트 연결 종료");
        if (activeClient == client) activeClient = null;
        client.Close();
    }

    void OnApplicationQuit()
    {
        tcpListener?.Stop();
    }
}