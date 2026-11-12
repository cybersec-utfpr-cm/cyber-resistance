using System.Collections.Generic;
using Godot;

/// Host -> broadcast in-game (receiver filtra por TargetId).
/// Atribui dupla (Team), papel inicial (DOC ou QUIZ), parceiro e tarefa ativa.
/// Port em C# de prototype/scripts/in_game_packets/minigame_assign_packet.gd.
public class MinigameAssignPkt : InGameTypeClass
{
	public enum ROLE
	{
		DOC = 0,
		QUIZ = 1
	}

	public int TargetId;
	public int Team;
	public int Role;
	public int PartnerId;
	public List<int> MemberIds = new();
	public string ActivityId = "";

	public MinigameAssignPkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_ASSIGN,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameAssignPkt Create(
		int targetId,
		int team,
		int role,
		int partnerId,
		IList<int> memberIds,
		string activityId = ""
	)
	{
		var pkt = new MinigameAssignPkt(PACKET_TYPE.MINIGAME_ASSIGN, (int)ENetPacketPeer.FlagReliable)
		{
			TargetId = targetId,
			Team = team,
			Role = role,
			PartnerId = partnerId,
			MemberIds = new List<int>(memberIds),
			ActivityId = activityId ?? ""
		};
		return pkt;
	}

	public static MinigameAssignPkt CreateFromData(byte[] data)
	{
		var pkt = new MinigameAssignPkt(PACKET_TYPE.MINIGAME_ASSIGN, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode())
		{
			(byte)TargetId,
			(byte)Team,
			(byte)Role,
			(byte)PartnerId,
			(byte)MemberIds.Count
		};
		foreach (int id in MemberIds)
		{
			buf.Add((byte)id);
		}
		byte[] actBytes = PacketCodec.Utf8(ActivityId);
		buf.Add((byte)actBytes.Length);
		buf.AddRange(actBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		TargetId = (int)PacketCodec.DecodeU8(data, 1);
		Team = (int)PacketCodec.DecodeU8(data, 2);
		Role = (int)PacketCodec.DecodeU8(data, 3);
		PartnerId = (int)PacketCodec.DecodeU8(data, 4);
		int count = (int)PacketCodec.DecodeU8(data, 5);
		MemberIds = new List<int>(count);
		for (int i = 0; i < count; i++)
		{
			MemberIds.Add((int)PacketCodec.DecodeU8(data, 6 + i));
		}
		int offset = 6 + count;
		if (data.Length > offset)
		{
			int actSize = (int)PacketCodec.DecodeU8(data, offset);
			if (data.Length >= offset + 1 + actSize)
			{
				ActivityId = PacketCodec.FromUtf8(data, offset + 1, actSize);
			}
		}
	}
}
