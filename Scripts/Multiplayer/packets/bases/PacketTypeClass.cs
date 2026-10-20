using Godot;

/// Base de todos os pacotes da camada 1 (lobby). Byte 0 = packet_type.
public class PacketTypeClass
{
	public enum PACKET_TYPE
	{
		PEER_ID = 0,
		ROOM_REQUEST = 5,
		START_ROOM = 10,
		JOIN_REQUEST = 15,
		JOIN_ROOM = 20,
		HAS_JOINED = 21,
		HAS_QUITTED = 22,
		QUIT_ROOM_REQUEST = 25,
		QUIT_ROOM = 30,
		REFRESH = 40,
		REFRESH_REQUEST = 50,
		ROOM_INFO = 60,
		MINIGAME_SUBMISSION = 70,
		PROFESSOR_HELLO = 80,
		MINIGAME_GRADE = 90
	}

	public PACKET_TYPE PacketType { get; protected set; }
	public int Flag { get; protected set; }

	public PacketTypeClass(
		PACKET_TYPE packetType = PACKET_TYPE.PEER_ID,
		int flag = (int)ENetPacketPeer.FlagReliable
	)
	{
		PacketType = packetType;
		Flag = flag;
	}

	public virtual byte[] Encode()
	{
		byte[] data = new byte[1];
		PacketCodec.EncodeU8(data, 0, (long)PacketType);
		return data;
	}

	public virtual void Decode(byte[] data)
	{
		PacketType = (PACKET_TYPE)data[0];
	}

	public void Send(ENetPacketPeer target)
	{
		target.Send(0, Encode(), Flag);
	}

	public void Broadcast(ENetConnection server)
	{
		server.Broadcast(0, Encode(), Flag);
	}
}
