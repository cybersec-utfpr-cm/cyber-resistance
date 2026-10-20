using Godot;

public class RefreshRequestClass : PacketTypeClass
{
	public RefreshRequestClass(
		PACKET_TYPE packetType = PACKET_TYPE.REFRESH_REQUEST,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static RefreshRequestClass Create()
	{
		return new RefreshRequestClass(
			PACKET_TYPE.REFRESH_REQUEST,
			(int)ENetPacketPeer.FlagReliable
		);
	}

	public static RefreshRequestClass CreateFromData(byte[] data)
	{
		var newRefreshReq = new RefreshRequestClass(
			PACKET_TYPE.REFRESH_REQUEST,
			(int)ENetPacketPeer.FlagReliable
		);
		newRefreshReq.Decode(data);
		return newRefreshReq;
	}
}
