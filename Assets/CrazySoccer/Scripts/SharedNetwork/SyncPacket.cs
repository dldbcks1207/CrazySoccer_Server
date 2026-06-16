using System.Collections.Generic;
using System.IO;

public class WorldSyncPacket
{
    public short Size;
    public PacketType Type = PacketType.SyncWorld;
    public float BallX; public float BallY;

    public struct PlayerData
    {
        public ushort PlayerID;
        public float PlayerX; public float PlayerY;
    }
    public List<PlayerData> PlayerList = new List<PlayerData>();

    public byte[] Serialize()
    {
        Size = (short)(14 + (PlayerList.Count * 10));

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(BallX); // 4Byte
            bw.Write(BallY); // 4Byte

            bw.Write((ushort)PlayerList.Count); // 2Byte
            foreach(PlayerData p in PlayerList)
            {
                bw.Write(p.PlayerID); // 2Byte
                bw.Write(p.PlayerX); // 4Byte
                bw.Write(p.PlayerY); // 4Byte
            }

            return ms.ToArray(); // 14 + (PlayerList.Count * 10) Byte
        }
    }
}