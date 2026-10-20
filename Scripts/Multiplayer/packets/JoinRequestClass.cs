using Godot;

public class JoinRequestClass : PacketTypeClass
{
	public int Room;
	public int PlayerId;
	public string PlayerName;

	public JoinRequestClass(
		PACKET_TYPE packetType = PACKET_TYPE.JOIN_REQUEST,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static JoinRequestClass Create(int roomId, int clientId, string name)
	{
		var packet = new JoinRequestClass(PACKET_TYPE.JOIN_REQUEST, (int)ENetPacketPeer.FlagReliable)
		{
			Room = roomId,
			PlayerId = clientId,
			PlayerName = name
		};
		return packet;
	}

	public static JoinRequestClass CreateFromData(byte[] data)
	{
		var newClass = new JoinRequestClass(PACKET_TYPE.JOIN_REQUEST, (int)ENetPacketPeer.FlagReliable);
		newClass.Decode(data);
		return newClass;
	}

	public override byte[] Encode()
	{
		byte[] baseBuf = PacketCodec.Resize(base.Encode(), 3);
		PacketCodec.EncodeU8(baseBuf, 1, PlayerId);
		PacketCodec.EncodeU8(baseBuf, 2, Room);
		byte[] nameBytes = PacketCodec.Utf8(PlayerName);
		var buf = new System.Collections.Generic.List<byte>(baseBuf);
		buf.Add((byte)nameBytes.Length);
		buf.AddRange(nameBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		PlayerId = (int)PacketCodec.DecodeU8(data, 1);
		Room = (int)PacketCodec.DecodeU8(data, 2);
		int nameSize = (int)PacketCodec.DecodeU8(data, 3);
		PlayerName = PacketCodec.FromUtf8(data, 4, nameSize);
	}
}
