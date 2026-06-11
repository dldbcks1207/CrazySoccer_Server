using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using UnityEngine;

public class ServerNetwork : MonoBehaviour
{
    private TcpListener tcpListener;

    // 클라이언트의 최신 입력 상태를 보관하는 메모장
    private float currentHorizontalInput = 0f;
    private bool currentJumpInput = false;

    // 백그라운드 스레드에서 메인 스레드로 로그 등을 넘길 주문서 레일(큐)
    private ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    // [★추가] 현재 서버에 접속한 클라이언트를 기억해 둘 변수
    // (이 변수가 있어야 FixedUpdate에서 클라이언트에게 좌표를 되돌려줄 수 있습니다!)
    private TcpClient activeClient;

    // 서버 월드에 존재하는 진짜 캐릭터의 Rigidbody2D 및 이동 속도
    public Rigidbody2D serverPlayerRigidbody;
    public Transform soccerBallTransform;
    public float moveSpeed = 5f;

    void Start()
    {
        // 9000번 포트로 모든 IP의 접속을 대기합니다.
        tcpListener = new TcpListener(IPAddress.Any, 9000);
        tcpListener.Start();
        Debug.Log("서버 권위형 하이브리드 대비 물리 서버 가동... 클라이언트를 기다립니다.");

        // 클라이언트 접속 대기 예약
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void OnAcceptClient(IAsyncResult ar)
    {
        TcpClient client = tcpListener.EndAcceptTcpClient(ar);

        // [★핵심] 방금 접속한 따끈따끈한 클라이언트를 활성 클라이언트로 임명합니다!
        activeClient = client;

        mainThreadQueue.Enqueue(() =>
        {
            Debug.Log("클라이언트 조종기가 서버에 안전하게 바인딩되었습니다!");
        });

        // 이 클라이언트의 귀(수신 루프)를 열어줍니다.
        ReceiveLoop(client);

        // 다음 손님을 위해 문을 다시 열어둡니다.
        tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
    }

    private void ReceiveLoop(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[7];

        stream.BeginRead(buffer, 0, buffer.Length, OnReadComplete, new object[] { stream, buffer, client });
    }

    private void OnReadComplete(IAsyncResult ar)
    {
        object[] state = (object[])ar.AsyncState;
        NetworkStream stream = (NetworkStream)state[0];
        byte[] buffer = (byte[])state[1];
        TcpClient client = (TcpClient)state[2];

        try
        {
            int bytesRead = stream.EndRead(ar);

            // 연결이 끊어졌다면 청소하고 종료
            if (bytesRead == 0)
            {
                Debug.Log("클라이언트 연결 종료");
                if (activeClient == client) activeClient = null;
                client.Close();
                return;
            }

            // 7바이트 상자 까기 (역직렬화)
            PacketType packetType = (PacketType)BitConverter.ToInt16(buffer, 0);

            switch (packetType)
            {
                case PacketType.MoveInput:
                    MovePacket move = MovePacket.Deserialize(buffer);
                    currentHorizontalInput = move.HorizontalInput;
                    if (move.IsJump) currentJumpInput = true;
                    break;
            }

            // 다음 패킷을 읽기 위해 무한 대기 순환 구조 활성화
            stream.BeginRead(buffer, 0, buffer.Length, OnReadComplete, state);
        }
        catch (Exception ex)
        {
            Debug.LogError($"수신 에러: {ex.Message}");
            if (activeClient == client) activeClient = null;
            client.Close();
        }
    }

    void Update()
    {
        // 메인 스레드 큐에 쌓인 작업(로그 출력 등) 처리
        while (mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    // =========================================================
    // [서버 권위의 심장] 물리 엔진 연산 및 좌표 브로드캐스트
    // =========================================================
    void FixedUpdate()
    {
        if (serverPlayerRigidbody == null) return;

        // 1. 메모장에 적힌 최신 입력값으로 X축 물리 속도를 결정합니다.
        float targetVelocityX = currentHorizontalInput * moveSpeed;
        serverPlayerRigidbody.linearVelocity = new Vector2(targetVelocityX, serverPlayerRigidbody.linearVelocity.y);

        // 2. 만약 점프 기록이 남아있다면 서버가 힘을 팍 줍니다.
        if (currentJumpInput)
        {
            serverPlayerRigidbody.AddForce(Vector2.up * 7f, ForceMode2D.Impulse);
            currentJumpInput = false; // 점프를 소화했으니 즉시 소모(청소)합니다.
        }

        // 3. [★핵심 추가] 물리 계산이 끝난 따끈따끈한 '진짜 좌표'를 클라이언트에게 전송합니다!
        if (activeClient != null && activeClient.Connected)
        {
            SyncPacket sync = new SyncPacket();
            sync.PlayerX = serverPlayerRigidbody.position.x; // 서버 월드의 현재 X 좌표
            sync.PlayerY = serverPlayerRigidbody.position.y; // 서버 월드의 현재 Y 좌표
            sync.BallX = soccerBallTransform.position.x;
            sync.BallY = soccerBallTransform.position.y;
            byte[] syncBytes = sync.Serialize(); // 10바이트 동전으로 압축!
            NetworkStream stream = activeClient.GetStream();
            stream.Write(syncBytes, 0, syncBytes.Length); // 랜선 너머 클라이언트에게 발사!
        }
    }

    void OnApplicationQuit()
    {
        tcpListener?.Stop();
    }
}