using System.IO;

public class MovePacket
{
    public short Size = 9;
    public PacketType Type = PacketType.MoveInput;
    public float HorizontalInput;
    public bool IsJump;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(HorizontalInput); // 4Byte
            bw.Write(IsJump); // 1Byte

            return ms.ToArray(); // 9Byte
        }
    }
}

public class KickPacket
{
    public short Size = 7;
    public PacketType Type = PacketType.KickInput;
    public byte Force;
    public bool IsDriven;
    public bool IsDirectionLeft;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(Force); // 1Byte
            bw.Write(IsDriven); // 1Byte
            bw.Write(IsDirectionLeft); // 1Byte
            return ms.ToArray(); // 7Byte
        }
    }
}

public class SendAnimationPacket
{
    public short Size = 5;
    public PacketType Type = PacketType.AnimationInput;
    public byte animNum;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(animNum); // 1Byte

            return ms.ToArray(); // 5Byte
        }
    }
}

public class ReturnAnimationPacket
{
    public short Size = 7;
    public PacketType Type = PacketType.AnimationPacket;
    public ushort playerID;
    public byte animNum;

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(playerID); // 2Byte
            bw.Write(animNum); // 1Byte

            return ms.ToArray(); // 7Byte
        }
    }
}