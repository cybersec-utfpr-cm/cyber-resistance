using System.Collections.Generic;
using Godot;

/// Host -> broadcast in-game (receiver filtra por TeamIndex).
/// Entrega a nota e comentário enviados pelo professor para a dupla.
/// Port em C# de prototype/scripts/in_game_packets/minigame_grade_result_packet.gd.
public class MinigameGradeResultPkt : InGameTypeClass
{
	public int TeamIndex;
	public float Grade;
	public string Comment = "";

	public MinigameGradeResultPkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_GRADE_RESULT,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameGradeResultPkt Create(int teamIndex, float grade, string comment)
	{
		return new MinigameGradeResultPkt(PACKET_TYPE.MINIGAME_GRADE_RESULT, (int)ENetPacketPeer.FlagReliable)
		{
			TeamIndex = teamIndex,
			Grade = grade,
			Comment = comment ?? ""
		};
	}

	public static MinigameGradeResultPkt CreateFromData(byte[] data)
	{
		var pkt = new MinigameGradeResultPkt(PACKET_TYPE.MINIGAME_GRADE_RESULT, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode())
		{
			(byte)TeamIndex
		};
		byte[] gradeBuf = new byte[4];
		PacketCodec.EncodeFloat(gradeBuf, 0, Grade);
		buf.AddRange(gradeBuf);
		byte[] commentBytes = PacketCodec.Utf8(Comment);
		byte[] sizeBuf = new byte[2];
		PacketCodec.EncodeU16(sizeBuf, 0, commentBytes.Length);
		buf.AddRange(sizeBuf);
		buf.AddRange(commentBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		TeamIndex = (int)PacketCodec.DecodeU8(data, 1);
		Grade = PacketCodec.DecodeFloat(data, 2);
		int commentSize = (int)PacketCodec.DecodeU16(data, 6);
		Comment = PacketCodec.FromUtf8(data, 8, commentSize);
	}
}
