using System.IO;

public class PlayerSyncPacket
{
    public short Size = 14;
    public PacketType Type = PacketType.SyncPlayerPosition;
    public ushort PlayerID;
    public float PlayerX; public float PlayerY;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(PlayerID); // 2Byte
            bw.Write(PlayerX); // 4Byte
            bw.Write(PlayerY); // 4Byte

            return ms.ToArray(); // 14Byte
        }
    }
}

public class BallSyncPacket
{
    public short Size = 12;
    public PacketType Type = PacketType.SyncBallPosition;
    public float BallX; public float BallY;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter br = new BinaryWriter(ms))
        {
            br.Write(Size); // 2Byte
            br.Write((short)Type); // 2Byte
            br.Write(BallX); // 4Byte
            br.Write(BallY); // 4Byte

            return ms.ToArray(); // 12Byte
        }
    }
}
