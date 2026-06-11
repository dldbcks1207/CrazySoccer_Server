using System.IO;

public class MovePacket
{
    public PacketType Type = PacketType.MoveInput;

    public float HorizontalInput;
    public bool IsJump;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((short)Type);
            bw.Write(HorizontalInput);
            bw.Write(IsJump);

            return ms.ToArray();
        }
    }

    public static MovePacket Deserialize(byte[] data)
    {
        MovePacket packet = new MovePacket();

        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader br = new BinaryReader(ms))
        {
            packet.Type = (PacketType)br.ReadInt16();
            packet.HorizontalInput = br.ReadSingle();
            packet.IsJump = br.ReadBoolean();
        }

        return packet;
    }
}
