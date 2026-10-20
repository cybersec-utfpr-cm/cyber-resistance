using System.Collections.Generic;
using Godot;

// Host -> servidor central.
// Disparado quando AS DUAS duplas da sala finalizam as perguntas. Carrega as
// respostas e os tempos de cada dupla para o professor avaliar. Sem nota — so
// texto puro + tempos. A nota volta depois via outro pacote.
//
// Layout do payload (tudo apos o byte 0 com o packet_type):
//   [u8 room_id]
//   [u8 num_teams]                  # sempre 2 hoje
//   por dupla:
//     [u8 num_members][u8 id]*N     # ids dos integrantes (2)
//     [u32 total_msec]              # tempo total da dupla
//     [u8 num_answers]              # sempre = num_questions hoje
//     por resposta:
//       [u32 submission_msec]
//       [u8 answer_size][answer_bytes...]
public class MinigameSubmissionPkt : PacketTypeClass
{
	public int RoomId;
	// Cada entrada: { members: Array[int], total_msec: int,
	//                 answers: Array[String], submission_msecs: Array[int] }
	public Godot.Collections.Array<Godot.Collections.Dictionary> Teams = new();

	public MinigameSubmissionPkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_SUBMISSION,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameSubmissionPkt Create(
		int roomId,
		Godot.Collections.Array<Godot.Collections.Dictionary> teams
	)
	{
		var newPacket = new MinigameSubmissionPkt(
			PACKET_TYPE.MINIGAME_SUBMISSION,
			(int)ENetPacketPeer.FlagReliable
		)
		{
			RoomId = roomId,
			Teams = teams
		};
		return newPacket;
	}

	public static MinigameSubmissionPkt CreateFromData(byte[] data)
	{
		var newPacket = new MinigameSubmissionPkt(
			PACKET_TYPE.MINIGAME_SUBMISSION,
			(int)ENetPacketPeer.FlagReliable
		);
		newPacket.Decode(data);
		return newPacket;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode());
		buf.Add((byte)RoomId);
		buf.Add((byte)Teams.Count);
		foreach (Godot.Collections.Dictionary team in Teams)
		{
			var members = team["members"].AsGodotArray();
			buf.Add((byte)members.Count);
			foreach (Variant member in members)
			{
				buf.Add((byte)member.AsInt64());
			}
			byte[] totalBuf = new byte[4];
			PacketCodec.EncodeU32(totalBuf, 0, team["total_msec"].AsInt64());
			buf.AddRange(totalBuf);
			var answers = team["answers"].AsGodotArray();
			var submissionMsecs = team["submission_msecs"].AsGodotArray();
			buf.Add((byte)answers.Count);
			for (int i = 0; i < answers.Count; i++)
			{
				byte[] msBuf = new byte[4];
				PacketCodec.EncodeU32(msBuf, 0, submissionMsecs[i].AsInt64());
				buf.AddRange(msBuf);
				byte[] answerBytes = PacketCodec.Utf8(answers[i].AsString());
				buf.Add((byte)answerBytes.Length);
				buf.AddRange(answerBytes);
			}
		}
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		int offset = 1;
		RoomId = (int)PacketCodec.DecodeU8(data, offset);
		offset += 1;
		int numTeams = (int)PacketCodec.DecodeU8(data, offset);
		offset += 1;
		Teams = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		for (int t = 0; t < numTeams; t++)
		{
			int numMembers = (int)PacketCodec.DecodeU8(data, offset);
			offset += 1;
			var members = new Godot.Collections.Array();
			for (int m = 0; m < numMembers; m++)
			{
				members.Add((int)PacketCodec.DecodeU8(data, offset));
				offset += 1;
			}
			long totalMsec = PacketCodec.DecodeU32(data, offset);
			offset += 4;
			int numAnswers = (int)PacketCodec.DecodeU8(data, offset);
			offset += 1;
			var answers = new Godot.Collections.Array();
			var submissionMsecs = new Godot.Collections.Array();
			for (int a = 0; a < numAnswers; a++)
			{
				submissionMsecs.Add((int)PacketCodec.DecodeU32(data, offset));
				offset += 4;
				int answerSize = (int)PacketCodec.DecodeU8(data, offset);
				offset += 1;
				answers.Add(PacketCodec.FromUtf8(data, offset, answerSize));
				offset += answerSize;
			}
			Teams.Add(new Godot.Collections.Dictionary
			{
				{ "members", members },
				{ "total_msec", totalMsec },
				{ "answers", answers },
				{ "submission_msecs", submissionMsecs }
			});
		}
	}
}
