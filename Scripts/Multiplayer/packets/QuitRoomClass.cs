using Godot;

public class QuitRoomClass : PacketTypeClass
{
	public QuitRoomClass(
		PACKET_TYPE packetType = PACKET_TYPE.QUIT_ROOM,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static QuitRoomClass Create()
	{
		return new QuitRoomClass(PACKET_TYPE.QUIT_ROOM);
	}

	public static QuitRoomClass CreateFromData(byte[] data)
	{
		var newPacket = new QuitRoomClass(PACKET_TYPE.QUIT_ROOM);
		newPacket.Decode(data);
		return newPacket;
	}
}
