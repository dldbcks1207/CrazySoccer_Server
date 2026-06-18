using UnityEngine;
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Sessions; // ★ 세션 네임스페이스
using System;

public class ServerEOSManager : MonoBehaviour
{
    public static ServerEOSManager Instance;

    [Header("서버 설정")]
    public string serverVersion = "v1.0"; // 클라이언트의 clientVersion과 일치해야 매칭됨

    private ProductUserId serverUserId;
    private SessionsInterface sessionsInterface;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 서버 켜지고 1초 뒤 에픽 서버 관리자 로그인 시작
        Invoke("LoginAsServer", 1.0f);
    }

    private void LoginAsServer()
    {
        Debug.Log("[SERVER] 데디케이티드 서버 관리자 로그인 시도 중...");
        ConnectInterface connectInterface = EOSManager.Instance.GetEOSPlatformInterface().GetConnectInterface();

        var createDeviceOptions = new CreateDeviceIdOptions { DeviceModel = "DedicatedServer" };

        connectInterface.CreateDeviceId(ref createDeviceOptions, null, (ref CreateDeviceIdCallbackInfo createData) =>
        {
            if (createData.ResultCode == Result.Success || createData.ResultCode == Result.DuplicateNotAllowed)
            {
                Debug.Log($"[SERVER] 디바이스 ID 확인 통과! 인증을 시작합니다...");

                var loginOptions = new LoginOptions
                {
                    Credentials = new Credentials { Token = null, Type = ExternalCredentialType.DeviceidAccessToken },
                    UserLoginInfo = new UserLoginInfo { DisplayName = "CrazySoccer_DedicatedServer" }
                };

                connectInterface.Login(ref loginOptions, null, (ref LoginCallbackInfo loginData) =>
                {
                    if (loginData.ResultCode == Result.Success)
                    {
                        Debug.Log($"[SERVER] ✅ 로그인 성공! 서버 ID: {loginData.LocalUserId}");
                        serverUserId = loginData.LocalUserId;

                        sessionsInterface = EOSManager.Instance.GetEOSPlatformInterface().GetSessionsInterface();
                        CreateGameSession();
                    }
                    else
                    {
                        Debug.LogError($"[SERVER] ❌ Connect 로그인 단계에서 막힘: {loginData.ResultCode}");
                    }
                });
            }
            else
            {
                // [★추가된 핵심 코드] 여기서 막히면 무슨 에러인지 뱉어라!
                Debug.LogError($"[SERVER] ❌ 디바이스 ID 생성 단계에서 막힘: {createData.ResultCode}");
            }
        });
    }

    private void CreateGameSession()
    {
        string serverIP = "192.168.0.109";
        string serverPort = "7777";

        // 빌드된 .exe 실행 시 배정받은 IP/Port 파싱 (-ip 1.2.3.4 -port 7777 형태)
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-ip" && i + 1 < args.Length) serverIP = args[i + 1];
            if (args[i] == "-port" && i + 1 < args.Length) serverPort = args[i + 1];
        }

        Debug.Log($"[SERVER] 에픽 세션 생성 시작 -> {serverIP}:{serverPort}");

        // 1. 세션 수정을 위한 빌더 핸들 생성
        var modOptions = new CreateSessionModificationOptions
        {
            SessionName = "CrazySoccer_Match",
            BucketId = "CrazySoccerMatches",
            MaxPlayers = 2,
            LocalUserId = serverUserId
        };
        sessionsInterface.CreateSessionModification(ref modOptions, out SessionModification sessionModHandle);

        // 2. 호스트 주소(IP) 설정
        var settingsOptions = new SessionModificationSetHostAddressOptions { HostAddress = serverIP };
        sessionModHandle.SetHostAddress(ref settingsOptions);

        // 3. 경기 도중 난입 불가 설정
        var joinInProgressOptions = new SessionModificationSetJoinInProgressAllowedOptions { AllowJoinInProgress = false };
        sessionModHandle.SetJoinInProgressAllowed(ref joinInProgressOptions);

        // 4. 클라이언트 필터링용 속성(Attribute) 주입
        AddSessionAttribute(sessionModHandle, "Version", serverVersion);
        AddSessionAttribute(sessionModHandle, "ServerIP", serverIP);
        AddSessionAttribute(sessionModHandle, "ServerPort", serverPort);

        // 5. 최종 세션 생성 및 에픽 백엔드에 반영
        var updateOptions = new UpdateSessionOptions { SessionModificationHandle = sessionModHandle };
        sessionsInterface.UpdateSession(ref updateOptions, null, (ref UpdateSessionCallbackInfo data) =>
        {
            if (data.ResultCode == Result.Success)
            {
                ServerManager.Instance.StartServer(int.Parse(serverPort));
            }
            else
            {
                Debug.LogError($"[SERVER] ❌ 세션 등록 실패: {data.ResultCode}");
            }
        });
    }

    private void AddSessionAttribute(SessionModification handle, string key, string value)
    {
        var attrData = new AttributeData { Key = key, Value = value };
        var addOptions = new SessionModificationAddAttributeOptions
        {
            SessionAttribute = attrData,
            AdvertisementType = SessionAttributeAdvertisementType.Advertise
        };
        handle.AddAttribute(ref addOptions);
    }

    void OnApplicationQuit()
    {
        if (sessionsInterface != null && serverUserId != null)
        {
            var destroyOptions = new DestroySessionOptions { SessionName = "CrazySoccer_Match" };
            sessionsInterface.DestroySession(ref destroyOptions, null, (ref DestroySessionCallbackInfo data) =>
            {
                Debug.Log("[SERVER] 서버 종료로 인한 에픽 세션 파괴 완료.");
            });
        }
    }
}