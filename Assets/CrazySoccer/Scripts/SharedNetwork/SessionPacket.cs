using System.Drawing;
using System.IO;
using UnityEngine;

public class SendSessionPacket
{
    public short Size = 16; // 2Byte
    public PacketType Type = PacketType.SendSession; // 2Byte
    public ulong SessionID; // 8Byte
    public ushort PlayerID; // 2Byte
    public ushort PlayerNum; // 2Byte

    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(SessionID); // 8Byte
            bw.Write(PlayerID); // 2Byte
            bw.Write(PlayerNum); //2Byte

            return ms.ToArray(); // 16Byte
        }
    }
}

public class NewSessionConnectPacket
{
    public short Size = 6; // 2Byte
    public PacketType Type = PacketType.NewSessionConnect; // 2Byte
    public ushort PlayerID; // 2Byte
    
    public byte[] Serialize()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(PlayerID); // 2Byte

            return ms.ToArray();
        }
    }
}