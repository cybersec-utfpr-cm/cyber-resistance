using Godot;

public class StartRoomClass : PacketTypeClass
{
	public int Requester;
	public int Room;

	public StartRoomClass(
		PACKET_TYPE packetType = PACKET_TYPE.START_ROOM,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static StartRoomClass Create(int roomId, int requesterId)
	{
		var newPacket = new StartRoomClass(PACKET_TYPE.START_ROOM, (int)ENetPacketPeer.FlagReliable)
		{
			Requester = requesterId,
			Room = roomId
		};
		return newPacket;
	}

	public static StartRoomClass CreateFromData(byte[] data)
	{
		var newPacket = new StartRoomClass(PACKET_TYPE.START_ROOM, (int)ENetPacketPeer.FlagReliable);
		newPacket.Decode(data);
		return newPacket;
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		Requester = (int)PacketCodec.DecodeU8(data, 1);
		Room = (int)PacketCodec.DecodeU8(data, 2);
	}

	public override byte[] Encode()
	{
		byte[] newPacked = PacketCodec.Resize(base.Encode(), 3);
		PacketCodec.EncodeU8(newPacked, 1, Requester);
		PacketCodec.EncodeU8(newPacked, 2, Room);
		return newPacked;
	}
}
