using Godot;

public class PeerId : PacketTypeClass
{
	public int Id;

	public PeerId(
		PACKET_TYPE packetType = PACKET_TYPE.PEER_ID,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static PeerId Create(int clientId)
	{
		var packet = new PeerId(PACKET_TYPE.PEER_ID, (int)ENetPacketPeer.FlagReliable)
		{
			Id = clientId
		};
		return packet;
	}

	public static PeerId CreateFromData(byte[] data)
	{
		var newClass = new PeerId(PACKET_TYPE.PEER_ID, (int)ENetPacketPeer.FlagReliable);
		newClass.Decode(data);
		return newClass;
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
