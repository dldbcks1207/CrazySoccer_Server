public enum PacketType : short
{
    None = 0,
    SendSession = 1,
    NewSessionConnect = 2,
    MoveInput = 3,
    SyncWorld = 4,
    KickInput = 5,
    GoalEvent = 6,
    GameWait = 7,
    GameStart = 8,
    GameEnd = 9,
    AnimationPacket = 10,
    AnimationInput = 11
}

public static class NetworkConfig
{
    public const int HeaderSize = 4;
}
