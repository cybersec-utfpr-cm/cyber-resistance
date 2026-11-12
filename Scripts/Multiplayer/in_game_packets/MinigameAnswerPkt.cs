using System.Collections.Generic;
using Godot;

/// Player -> Host in-game.
/// Envia a resposta da pergunta/palavra atual da dupla.
/// Port em C# de prototype/scripts/in_game_packets/minigame_answer_packet.gd.
public class MinigameAnswerPkt : InGameTypeClass
{
	public int Team;
	public int QuestionIdx;
	public string Answer = "";

	public MinigameAnswerPkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_ANSWER,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameAnswerPkt Create(int team, int questionIdx, string answer)
	{
		return new MinigameAnswerPkt(PACKET_TYPE.MINIGAME_ANSWER, (int)ENetPacketPeer.FlagReliable)
		{
			Team = team,
			QuestionIdx = questionIdx,
			Answer = answer ?? ""
		};
	}

	public static MinigameAnswerPkt CreateFromData(byte[] data)
	{
		var pkt = new MinigameAnswerPkt(PACKET_TYPE.MINIGAME_ANSWER, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode())
		{
			(byte)Team,
			(byte)QuestionIdx
		};
		byte[] answerData = PacketCodec.Utf8(Answer);
		buf.Add((byte)answerData.Length);
		buf.AddRange(answerData);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		Team = (int)PacketCodec.DecodeU8(data, 1);
		QuestionIdx = (int)PacketCodec.DecodeU8(data, 2);
		int answerSize = (int)PacketCodec.DecodeU8(data, 3);
		Answer = PacketCodec.FromUtf8(data, 4, answerSize);
	}
}
