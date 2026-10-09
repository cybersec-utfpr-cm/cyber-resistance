class_name JoinRequestClass extends PacketTypeClass

var room: int
var player_id: int
var player_name: String

static func create(room_id: int, client_id: int, name: String) -> JoinRequestClass:
	var packet: JoinRequestClass = JoinRequestClass.new(PacketTypeClass.PACKET_TYPE.JOIN_REQUEST, ENetPacketPeer.FLAG_RELIABLE)
	packet.room = room_id
	packet.player_id = client_id
	packet.player_name = name
	return packet

static func create_from_data(data: PackedByteArray) -> JoinRequestClass:
	var new_class: JoinRequestClass = JoinRequestClass.new(PacketTypeClass.PACKET_TYPE.JOIN_REQUEST, ENetPacketPeer.FLAG_RELIABLE)
	new_class.decode(data)
	return new_class

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = super.encode()
	new_array.resize(3)
	new_array.encode_u8(1, player_id)
	new_array.encode_u8(2, room)
	var name_bytes: PackedByteArray = player_name.to_utf8_buffer()
	new_array.append(name_bytes.size())
	new_array.append_array(name_bytes)
	return new_array

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	player_id = data.decode_u8(1)
	room = data.decode_u8(2)
	var name_size: int = data.decode_u8(3)
	player_name = data.slice(4, 4 + name_size).get_string_from_utf8()
