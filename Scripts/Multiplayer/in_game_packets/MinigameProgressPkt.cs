using System.Collections.Generic;
using Godot;

/// Host -> broadcast in-game (receiver filtra por Team).
/// Comunica o estado da dupla após cada resposta enviada: próxima pergunta,
/// quem assume o QUIZ, se terminou e a última resposta enviada (para atualizar a cruzadinha).
/// Port em C# de prototype/scripts/in_game_packets/minigame_progress_packet.gd.
public class MinigameProgressPkt : InGameTypeClass
{
	public int Team;
	public int QuestionIdx;
	public int QuizPlayerId;
	public int Finished;
	public string LastAnswer = "";

	public MinigameProgressPkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_PROGRESS,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameProgressPkt Create(
		int team,
		int questionIdx,
		int quizPlayerId,
		bool finished,
		string lastAnswer = ""
	)
	{
		return new MinigameProgressPkt(PACKET_TYPE.MINIGAME_PROGRESS, (int)ENetPacketPeer.FlagReliable)
		{
			Team = team,
			QuestionIdx = questionIdx,
			QuizPlayerId = quizPlayerId,
			Finished = finished ? 1 : 0,
			LastAnswer = lastAnswer ?? ""
		};
	}

	public static MinigameProgressPkt CreateFromData(byte[] data)
	{
		var pkt = new MinigameProgressPkt(PACKET_TYPE.MINIGAME_PROGRESS, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode())
		{
			(byte)Team,
			(byte)QuestionIdx,
			(byte)QuizPlayerId,
			(byte)Finished
		};
		byte[] ansBytes = PacketCodec.Utf8(LastAnswer);
		byte[] sizeBuf = new byte[2];
		PacketCodec.EncodeU16(sizeBuf, 0, ansBytes.Length);
		buf.AddRange(sizeBuf);
		buf.AddRange(ansBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		Team = (int)PacketCodec.DecodeU8(data, 1);
		QuestionIdx = (int)PacketCodec.DecodeU8(data, 2);
		QuizPlayerId = (int)PacketCodec.DecodeU8(data, 3);
		Finished = (int)PacketCodec.DecodeU8(data, 4);
		if (data.Length >= 7)
		{
			int ansSize = (int)PacketCodec.DecodeU16(data, 5);
			if (data.Length >= 7 + ansSize)
			{
				LastAnswer = PacketCodec.FromUtf8(data, 7, ansSize);
			}
		}
	}
}
