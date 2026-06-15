using System.Net.Sockets;

public class PlayerSession
{
    public ulong SessionID;
    public ushort PlayerID;
    public TcpClient Client;
    public NetworkStream Stream;

    //가비지 방지
    public byte[] HeaderBuffer = new byte[NetworkConfig.HeaderSize];
    public byte[] BodyBuffer;
}

