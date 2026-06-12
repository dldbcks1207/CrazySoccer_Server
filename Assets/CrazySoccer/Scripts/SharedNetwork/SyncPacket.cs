using System.IO;

public class SyncPacket
{
    public short Size = 20;
    public PacketType Type = PacketType.SyncPosition;
    public float PlayerX; public float PlayerY;
    public float BallX; public float BallY;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(PlayerX); // 4Byte
            bw.Write(PlayerY); // 4Byte
            bw.Write(BallX); // 4Byte
            bw.Write(BallY); // 4Byte

            return ms.ToArray(); // 20Byte
        }
    }

    /*
    public static SyncPacket Deserialize(byte[] data)
    {
        SyncPacket packet = new SyncPacket();

        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader br = new BinaryReader(ms))
        {
            packet.Type = (PacketType)br.ReadInt16();
            packet.PlayerX = br.ReadSingle();
            packet.PlayerY = br.ReadSingle();
            packet.BallX = br.ReadSingle();
            packet.BallY = br.ReadSingle();
        }

        return packet;
    }
    */
}
