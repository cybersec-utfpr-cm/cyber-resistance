class_name SceneSyncPacket extends InGameTypeClass

var peer_id: int
var scene_path: String

static func create(_peer_id: int, _scene_path: String) -> SceneSyncPacket:
	var new_packet: SceneSyncPacket = SceneSyncPacket.new(PACKET_TYPE.SCENE_SYNC_PACKET, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.peer_id = _peer_id
	new_packet.scene_path = _scene_path
	return new_packet

static func create_from_data(data: PackedByteArray) -> SceneSyncPacket:
	var new_packet: SceneSyncPacket = SceneSyncPacket.new(PACKET_TYPE.SCENE_SYNC_PACKET, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	var scene_data: PackedByteArray = scene_path.to_utf8_buffer()
	data.append(peer_id)
	data.append(scene_data.size())
	data.append_array(scene_data)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	peer_id = data.decode_u8(1)
	var scene_size: int = data.decode_u8(2)
	scene_path = data.slice(3, 3 + scene_size).get_string_from_utf8()
