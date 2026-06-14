using System.IO;

public class MovePacket
{
    public short Size = 9;
    public PacketType Type = PacketType.MoveInput;
    public ushort SessionID;
    public float HorizontalInput;
    public bool IsJump;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(SessionID); // 2Byte
            bw.Write(HorizontalInput); // 4Byte
            bw.Write(IsJump); // 1Byte

            return ms.ToArray(); // 11Byte
        }
    }
}
