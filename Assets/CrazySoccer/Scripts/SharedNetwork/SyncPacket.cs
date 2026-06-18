using System.Collections.Generic;
using System.IO;

public class WorldSyncPacket
{
    // ★ 14바이트 -> 18바이트로 증가 (타이머 4바이트 추가)
    public short Size;
    public PacketType Type = PacketType.SyncWorld;
    public float BallX; public float BallY;
    
    // ★ 추가: 서버의 현재 진행 시간
    public float MatchTimer; 

    public struct PlayerData
    {
        public ushort PlayerID;
        public float PlayerX; public float PlayerY;
    }
    public List<PlayerData> PlayerList = new List<PlayerData>();

    public byte[] Serialize()
    {
        // ★ 14에서 18로 변경!
        Size = (short)(18 + (PlayerList.Count * 10));

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(Size); // 2Byte
            bw.Write((short)Type); // 2Byte
            bw.Write(BallX); // 4Byte
            bw.Write(BallY); // 4Byte
            
            bw.Write(MatchTimer); // ★ 4Byte 추가!

            bw.Write((ushort)PlayerList.Count); // 2Byte
            foreach(PlayerData p in PlayerList)
            {
                bw.Write(p.PlayerID); // 2Byte
                bw.Write(p.PlayerX); // 4Byte
                bw.Write(p.PlayerY); // 4Byte
            }

            return ms.ToArray(); // 18 + (PlayerList.Count * 10) Byte
        }
    }
}