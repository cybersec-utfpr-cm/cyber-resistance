class_name RoomInfoClass extends PacketTypeClass

var player_id: int
var room_id: int
var room_port: int
var host_ip: String
var player_name: String

const HEADER_SIZE = 5

static func create(id: int, room: int, port: int, ip: String, name: String) -> RoomInfoClass:
	var new_packet: RoomInfoClass = RoomInfoClass.new(PACKET_TYPE.ROOM_INFO)
	new_packet.player_id = id
	new_packet.room_id = room
	new_packet.room_port = port
	new_packet.host_ip = ip
	new_packet.player_name = name
	return new_packet

static func create_from_data(data: PackedByteArray) -> RoomInfoClass:
	var new_packet: RoomInfoClass = RoomInfoClass.new(PACKET_TYPE.ROOM_INFO)
	new_packet.decode(data)
	return new_packet

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	room_id = data.decode_u8(1)
	player_id = data.decode_u8(2)
	room_port = data.decode_u16(3)
	var name_size: int = data.decode_u8(HEADER_SIZE)
	player_name = data.slice(HEADER_SIZE + 1, HEADER_SIZE + 1 + name_size).get_string_from_utf8()
	host_ip = data.slice(HEADER_SIZE + 1 + name_size).get_string_from_utf8()

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = PackedByteArray()
	var name_bytes: PackedByteArray = player_name.to_utf8_buffer()
	var ip_bytes: PackedByteArray = host_ip.to_utf8_buffer()
	new_array.resize(HEADER_SIZE)
	new_array.encode_u8(0, packet_type)
	new_array.encode_u8(1, room_id)
	new_array.encode_u8(2, player_id)
	new_array.encode_u16(3, room_port)
	new_array.append(name_bytes.size())
	new_array.append_array(name_bytes)
	new_array.append_array(ip_bytes)
	return new_array
