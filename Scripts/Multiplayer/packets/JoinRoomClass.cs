using System.Collections.Generic;
using Godot;

public class JoinRoomClass : PacketTypeClass
{
	public int RoomId;
	public int RoomPort;
	public List<int> RemoteIds = new();
	public string HostIp;

	public const int FixedHeaderSize = 6;

	public JoinRoomClass(
		PACKET_TYPE packetType = PACKET_TYPE.JOIN_ROOM,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static JoinRoomClass Create(
		int room,
		int port,
		string ip,
		List<int> remoteIds
	)
	{
		var newPacket = new JoinRoomClass(PACKET_TYPE.JOIN_ROOM)
		{
			RoomId = room,
			RoomPort = port,
			HostIp = ip,
			RemoteIds = new List<int>(remoteIds)
		};
		return newPacket;
	}

	public static JoinRoomClass CreateFromData(byte[] data)
	{
		var newPacket = new JoinRoomClass(PACKET_TYPE.JOIN_ROOM);
		newPacket.Decode(data);
		return newPacket;
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);

		RoomId = (int)PacketCodec.DecodeU8(data, 1);
		RoomPort = (int)PacketCodec.DecodeU16(data, 2);
		int remoteCount = (int)PacketCodec.DecodeU8(data, 4);

		RemoteIds.Clear();
		int currentOffset = 5;
		for (int i = 0; i < remoteCount; i++)
		{
			int remoteId = (int)PacketCodec.DecodeU8(data, currentOffset);
			RemoteIds.Add(remoteId);
			currentOffset += 1;
		}

		HostIp = PacketCodec.FromUtf8(data, currentOffset);
	}

	public override byte[] Encode()
	{
		byte[] ipBytes = PacketCodec.Utf8(HostIp);
		int idsCount = RemoteIds.Count;

		int totalSize = FixedHeaderSize + idsCount + ipBytes.Length;
		var newArray = new byte[totalSize];
		PacketCodec.EncodeU8(newArray, 0, (long)PacketType);
		PacketCodec.EncodeU8(newArray, 1, RoomId);
		PacketCodec.EncodeU16(newArray, 2, RoomPort);
		PacketCodec.EncodeU8(newArray, 4, idsCount);
		int currentOffset = 5;
		foreach (int id in RemoteIds)
		{
			PacketCodec.EncodeU8(newArray, currentOffset, id);
			currentOffset += 1;
		}
		for (int i = 0; i < ipBytes.Length; i++)
		{
			newArray[currentOffset + i] = ipBytes[i];
		}

		return newArray;
	}
}
