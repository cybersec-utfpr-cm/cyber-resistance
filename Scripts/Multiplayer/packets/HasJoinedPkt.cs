using System.Collections.Generic;
using Godot;

public class HasJoinedPkt : PacketTypeClass
{
	public List<int> RemoteIds = new();

	public const int MaxIds = 4;
	public const int HeaderSize = 2;

	public HasJoinedPkt(
		PACKET_TYPE packetType = PACKET_TYPE.HAS_JOINED,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static HasJoinedPkt Create(List<int> remoteIds)
	{
		var packet = new HasJoinedPkt(PACKET_TYPE.HAS_JOINED)
		{
			RemoteIds = new List<int>(remoteIds)
		};
		return packet;
	}

	public static HasJoinedPkt CreateFromData(byte[] data)
	{
		var packet = new HasJoinedPkt(PACKET_TYPE.HAS_JOINED);
		packet.Decode(data);
		return packet;
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		int remoteCount = (int)PacketCodec.DecodeU8(data, 1);

		RemoteIds.Clear();
		int actualCount = System.Math.Min(remoteCount, MaxIds);

		for (int i = 0; i < actualCount; i++)
		{
			RemoteIds.Add((int)PacketCodec.DecodeU8(data, 2 + i));
		}
	}

	public override byte[] Encode()
	{
		int idsCount = System.Math.Min(RemoteIds.Count, MaxIds);
		byte[] newArray = PacketCodec.Resize(base.Encode(), HeaderSize + idsCount);
		PacketCodec.EncodeU8(newArray, 1, idsCount);
		for (int i = 0; i < idsCount; i++)
		{
			PacketCodec.EncodeU8(newArray, 2 + i, RemoteIds[i]);
		}

		return newArray;
	}
}
