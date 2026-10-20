using System.Collections.Generic;
using Godot;

public class RefreshClass : PacketTypeClass
{
	public List<RoomSummary> Summaries = new();

	public RefreshClass(
		PACKET_TYPE packetType = PACKET_TYPE.REFRESH,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static RefreshClass Create(
		Dictionary<int, RoomStorage> roomsData,
		List<int> roomsId
	)
	{
		var refresh = new RefreshClass(PACKET_TYPE.REFRESH, (int)ENetPacketPeer.FlagReliable);
		refresh.Summaries = new List<RoomSummary>();
		foreach (int roomId in roomsId)
		{
			if (!roomsData.ContainsKey(roomId))
			{
				continue;
			}
			// Este create() so e chamado pelo server; client e professor usam
			// CreateFromData.
			RoomStorage room = roomsData[roomId];
			var summary = new RoomSummary
			{
				Id = roomId,
				PlayerCount = room.CurrentPlayers.Count,
				PlayerNames = new List<string>(room.CurrentPlayersNames)
			};
			refresh.Summaries.Add(summary);
		}
		return refresh;
	}

	public static RefreshClass CreateFromData(byte[] data)
	{
		var refresh = new RefreshClass(PACKET_TYPE.REFRESH, (int)ENetPacketPeer.FlagReliable);
		refresh.Decode(data);
		return refresh;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode());
		buf.Add((byte)Summaries.Count);
		foreach (RoomSummary summary in Summaries)
		{
			buf.Add((byte)summary.Id);
			buf.Add((byte)summary.PlayerCount);
			foreach (string name in summary.PlayerNames)
			{
				byte[] nameBytes = PacketCodec.Utf8(name);
				buf.Add((byte)nameBytes.Length);
				buf.AddRange(nameBytes);
			}
		}
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		Summaries = new List<RoomSummary>();
		int offset = 1;
		int numRooms = (int)PacketCodec.DecodeU8(data, offset);
		offset += 1;
		for (int r = 0; r < numRooms; r++)
		{
			var summary = new RoomSummary
			{
				Id = (int)PacketCodec.DecodeU8(data, offset)
			};
			offset += 1;
			summary.PlayerCount = (int)PacketCodec.DecodeU8(data, offset);
			offset += 1;
			summary.PlayerNames = new List<string>();
			for (int p = 0; p < summary.PlayerCount; p++)
			{
				int nameSize = (int)PacketCodec.DecodeU8(data, offset);
				offset += 1;
				summary.PlayerNames.Add(PacketCodec.FromUtf8(data, offset, nameSize));
				offset += nameSize;
			}
			Summaries.Add(summary);
		}
	}
}
