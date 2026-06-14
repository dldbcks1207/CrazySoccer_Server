using System.IO;

public class SyncPacket
{
    public short Size = 22;
    public PacketType Type = PacketType.SyncPosition;
    public ushort SessionID;
    public float PlayerX; public float PlayerY;
    public float BallX; public float BallY;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(SessionID); // 2Byte
            bw.Write(PlayerX); // 4Byte
            bw.Write(PlayerY); // 4Byte
            bw.Write(BallX); // 4Byte
            bw.Write(BallY); // 4Byte

            return ms.ToArray(); // 22Byte
        }
    }
}
