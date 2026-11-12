using System.Collections.Generic;
using Godot;

/// Host -> broadcast in-game para forçar todos os clientes a mudarem de cena.
/// Port em C# de prototype/scripts/in_game_packets/scene_force_packet.gd.
public class SceneForcePacket : InGameTypeClass
{
	public string ScenePath = "";

	public SceneForcePacket(
		PACKET_TYPE packetType = PACKET_TYPE.SCENE_FORCE_PACKET,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static SceneForcePacket Create(string scenePath)
	{
		return new SceneForcePacket(PACKET_TYPE.SCENE_FORCE_PACKET, (int)ENetPacketPeer.FlagReliable)
		{
			ScenePath = scenePath ?? ""
		};
	}

	public static SceneForcePacket CreateFromData(byte[] data)
	{
		var pkt = new SceneForcePacket(PACKET_TYPE.SCENE_FORCE_PACKET, (int)ENetPacketPeer.FlagReliable);
		pkt.Decode(data);
		return pkt;
	}

	public override byte[] Encode()
	{
		var buf = new List<byte>(base.Encode());
		byte[] sceneBytes = PacketCodec.Utf8(ScenePath);
		buf.Add((byte)sceneBytes.Length);
		buf.AddRange(sceneBytes);
		return buf.ToArray();
	}

	public override void Decode(byte[] data)
	{
		base.Decode(data);
		int sceneSize = (int)PacketCodec.DecodeU8(data, 1);
		ScenePath = PacketCodec.FromUtf8(data, 2, sceneSize);
	}
}
