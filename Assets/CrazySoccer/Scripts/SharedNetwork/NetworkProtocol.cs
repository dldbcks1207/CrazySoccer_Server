public enum PacketType : short
{
    None = 0,
    SendSession = 1,
    NewSessionConnect = 2,
    MoveInput = 3,
    SyncWorld = 4,
    KickInput = 5,
    GoalEvent = 6
}

public static class NetworkConfig
{
    public const int ServerPort = 9000;
    public const int HeaderSize = 4;
}
