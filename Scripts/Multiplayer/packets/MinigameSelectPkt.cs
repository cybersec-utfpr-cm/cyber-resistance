using Godot;

// Professor -> server -> host da sala (lobby).
// Define qual tarefa/atividade (activity_id do catalogo JSON) deve ser aplicada.
// RoomId = 255 aplica como padrao para todas as salas ativas e futuras.
//
// Layout: [u8 type = 85][u8 room_id][u8 activity_id_size][activity_id_bytes]
public class MinigameSelectPkt : PacketTypeClass
{
	public const int AllRooms = 255;

	public int RoomId = AllRooms;
	public string ActivityId = "";

	public MinigameSelectPkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_SELECT,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameSelectPkt Create(int roomId, string activityId)
	{
		return new MinigameSelectPkt(PACKET_TYPE.MINIGAME_SELECT, (int)ENetPacketPeer.FlagReliable)
		{
			RoomId = roomId < 0 ? AllRooms : roomId,
			ActivityId = activityId ?? ""
		};
	}

	public static MinigameSelectPkt CreateFromData(byte[] data)
	{
		var pkt = new MinigameSelectPkt(PACKET_TYPE.MINIGAME_SELECT, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new System.Collections.Generic.List<byte>(base.Encode());
		buf.Add((byte)RoomId);
		byte[] idBytes = PacketCodec.Utf8(ActivityId);
		buf.Add((byte)idBytes.Length);
		buf.AddRange(idBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		RoomId = (int)PacketCodec.DecodeU8(data, 1);
		int idSize = (int)PacketCodec.DecodeU8(data, 2);
		ActivityId = PacketCodec.FromUtf8(data, 3, idSize);
	}
}
