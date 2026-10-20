using Godot;

// Professor -> server -> host da sala (lobby).
// Nota + comentario de uma dupla especifica. Professor preenche na UI, envia
// ao server; server valida que veio do professor registrado e encaminha o
// MESMO pacote ao host da sala correspondente. O host entao traduz em
// MinigameGradeResultPkt (in-game) e faz broadcast pros players.
//
// Layout: [u8 type][u8 room_id][u8 team_index][f32 grade][u16 comment_size][comment_bytes]
public class MinigameGradePkt : PacketTypeClass
{
	public int RoomId;
	public int TeamIndex;
	public float Grade;
	public string Comment;

	public MinigameGradePkt(
		PACKET_TYPE packetType = PACKET_TYPE.MINIGAME_GRADE,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static MinigameGradePkt Create(
		int roomId,
		int teamIndex,
		float grade,
		string comment
	)
	{
		var pkt = new MinigameGradePkt(PACKET_TYPE.MINIGAME_GRADE, (int)ENetPacketPeer.FlagReliable)
		{
			RoomId = roomId,
			TeamIndex = teamIndex,
			Grade = grade,
			Comment = comment
		};
		return pkt;
	}

	public static MinigameGradePkt CreateFromData(byte[] data)
	{
		var pkt = new MinigameGradePkt(PACKET_TYPE.MINIGAME_GRADE, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new System.Collections.Generic.List<byte>(base.Encode());
		buf.Add((byte)RoomId);
		buf.Add((byte)TeamIndex);
		byte[] gradeBuf = new byte[4];
		PacketCodec.EncodeFloat(gradeBuf, 0, Grade);
		buf.AddRange(gradeBuf);
		byte[] commentBytes = PacketCodec.Utf8(Comment);
		byte[] sizeBuf = new byte[2];
		PacketCodec.EncodeU16(sizeBuf, 0, commentBytes.Length);
		buf.AddRange(sizeBuf);
		buf.AddRange(commentBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		RoomId = (int)PacketCodec.DecodeU8(data, 1);
		TeamIndex = (int)PacketCodec.DecodeU8(data, 2);
		Grade = PacketCodec.DecodeFloat(data, 3);
		int commentSize = (int)PacketCodec.DecodeU16(data, 7);
		Comment = PacketCodec.FromUtf8(data, 9, commentSize);
	}
}
