using Godot;

public class QuitRequestClass : PacketTypeClass
{
	public int RoomId;

	public QuitRequestClass(
		PACKET_TYPE packetType = PACKET_TYPE.QUIT_ROOM_REQUEST,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static QuitRequestClass Create(int roomId)
	{
		var newPacket = new QuitRequestClass(PACKET_TYPE.QUIT_ROOM_REQUEST)
		{
			RoomId = roomId
		};
		return newPacket;
	}

	public static QuitRequestClass CreateFromData(byte[] data)
	{
		var newPacket = new QuitRequestClass(PACKET_TYPE.QUIT_ROOM_REQUEST);
		newPacket.Decode(data);
		return newPacket;
	}

	public override byte[] Encode()
	{
		byte[] newArray = PacketCodec.Resize(base.Encode(), 2);
		PacketCodec.EncodeU8(newArray, 1, RoomId);
		return newArray;
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		RoomId = (int)PacketCodec.DecodeU8(data, 1);
	}
}
