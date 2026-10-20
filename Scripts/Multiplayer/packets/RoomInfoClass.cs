using Godot;

public class RoomInfoClass : PacketTypeClass
{
	public int PlayerId;
	public int RoomId;
	public int RoomPort;
	public string HostIp;
	public string PlayerName;

	public const int HeaderSize = 5;

	public RoomInfoClass(
		PACKET_TYPE packetType = PACKET_TYPE.ROOM_INFO,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static RoomInfoClass Create(
		int id,
		int room,
		int port,
		string ip,
		string name
	)
	{
		var newPacket = new RoomInfoClass(PACKET_TYPE.ROOM_INFO)
		{
			PlayerId = id,
			RoomId = room,
			RoomPort = port,
			HostIp = ip,
			PlayerName = name
		};
		return newPacket;
	}

	public static RoomInfoClass CreateFromData(byte[] data)
	{
		var newPacket = new RoomInfoClass(PACKET_TYPE.ROOM_INFO);
		newPacket.Decode(data);
		return newPacket;
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		RoomId = (int)PacketCodec.DecodeU8(data, 1);
		PlayerId = (int)PacketCodec.DecodeU8(data, 2);
		RoomPort = (int)PacketCodec.DecodeU16(data, 3);
		int nameSize = (int)PacketCodec.DecodeU8(data, HeaderSize);
		PlayerName = PacketCodec.FromUtf8(
			data, HeaderSize + 1, nameSize
		);
		HostIp = PacketCodec.FromUtf8(data, HeaderSize + 1 + nameSize);
	}

	public override byte[] Encode()
	{
		byte[] nameBytes = PacketCodec.Utf8(PlayerName);
		byte[] ipBytes = PacketCodec.Utf8(HostIp);
		var newArray = new byte[HeaderSize];
		PacketCodec.EncodeU8(newArray, 0, (long)PacketType);
		PacketCodec.EncodeU8(newArray, 1, RoomId);
		PacketCodec.EncodeU8(newArray, 2, PlayerId);
		PacketCodec.EncodeU16(newArray, 3, RoomPort);
		var buf = new System.Collections.Generic.List<byte>(newArray);
		buf.Add((byte)nameBytes.Length);
		buf.AddRange(nameBytes);
		buf.AddRange(ipBytes);
		return buf.ToArray();
	}
}
