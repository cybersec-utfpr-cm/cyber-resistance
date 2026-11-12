using Godot;

/// Base de todos os pacotes da camada 2 (in-game). Byte 0 = packet_type.
/// Port em C# de prototype/scripts/in_game_packets/base/player_base.gd.
public class InGameTypeClass
{
	public enum PACKET_TYPE
	{
		PLAYER_PACKET = 0,
		TEXT_PACKET = 10,
		SCENE_SYNC_PACKET = 11,
		SCENE_FORCE_PACKET = 12,
		MINIGAME_ASSIGN = 20,
		MINIGAME_ANSWER = 21,
		MINIGAME_PROGRESS = 22,
		MINIGAME_GRADE_RESULT = 23
	}

	public PACKET_TYPE PacketType { get; protected set; }
	public int Flag { get; protected set; }

	public InGameTypeClass(
		PACKET_TYPE packetType = PACKET_TYPE.PLAYER_PACKET,
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
