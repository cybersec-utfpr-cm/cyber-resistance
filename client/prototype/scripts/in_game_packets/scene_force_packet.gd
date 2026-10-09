class_name SceneForcePacket extends InGameTypeClass

var scene_path: String

static func create(_scene_path: String) -> SceneForcePacket:
	var new_packet: SceneForcePacket = SceneForcePacket.new(PACKET_TYPE.SCENE_FORCE_PACKET, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.scene_path = _scene_path
	return new_packet

static func create_from_data(data: PackedByteArray) -> SceneForcePacket:
	var new_packet: SceneForcePacket = SceneForcePacket.new(PACKET_TYPE.SCENE_FORCE_PACKET, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	var scene_data: PackedByteArray = scene_path.to_utf8_buffer()
	data.append(scene_data.size())
	data.append_array(scene_data)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	var scene_size: int = data.decode_u8(1)
	scene_path = data.slice(2, 2 + scene_size).get_string_from_utf8()
