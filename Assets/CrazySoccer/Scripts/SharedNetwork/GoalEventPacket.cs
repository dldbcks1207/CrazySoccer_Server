using System.IO;

public class GoalEventPacket
{
    public short Size = 6;
    public PacketType Type = PacketType.GoalEvent;
    public short ScoredTeam;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(ScoredTeam); // 2Byte
            return ms.ToArray(); // 6Byte
        }
    }
}
