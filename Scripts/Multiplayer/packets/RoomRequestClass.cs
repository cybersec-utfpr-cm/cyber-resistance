using Godot;

public class RoomRequestClass : PacketTypeClass
{
	public int Id;

	public RoomRequestClass(
		PACKET_TYPE packetType = PACKET_TYPE.ROOM_REQUEST,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static RoomRequestClass Create(int clientId)
	{
		var newPacket = new RoomRequestClass(PACKET_TYPE.ROOM_REQUEST) { Id = clientId };
		return newPacket;
	}

	public static RoomRequestClass CreateFromData(byte[] data)
	{
		var newPacket = new RoomRequestClass(PACKET_TYPE.ROOM_REQUEST);
		newPacket.Decode(data);
		return newPacket;
	}

	public override byte[] Encode()
	{
		byte[] newArray = PacketCodec.Resize(base.Encode(), 2);
		PacketCodec.EncodeU8(newArray, 1, Id);
		return newArray;
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		Id = (int)PacketCodec.DecodeU8(data, 1);
	}
}
