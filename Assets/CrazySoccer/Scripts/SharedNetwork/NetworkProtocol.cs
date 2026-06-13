public enum PacketType : short
{
    None = 0,
    MoveInput = 1,
    SyncPosition = 2,
    GoalEvent = 3
}

public static class NetworkConfig
{
    public const int ServerPort = 9000;
    public const int HeaderSize = 4;
}
