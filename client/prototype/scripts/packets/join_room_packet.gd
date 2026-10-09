class_name JoinRoomClass extends PacketTypeClass

var room_id: int
var room_port: int
var remote_ids: Array[int]
var host_ip: String

const FIXED_HEADER_SIZE = 6 

static func create(room: int, port: int, ip: String, _remote_ids: Array[int]) -> JoinRoomClass:
	var new_packet: JoinRoomClass = JoinRoomClass.new(PACKET_TYPE.JOIN_ROOM)
	new_packet.room_id = room
	new_packet.room_port = port
	new_packet.host_ip = ip
	new_packet.remote_ids = _remote_ids
	return new_packet

static func create_from_data(data: PackedByteArray) -> JoinRoomClass:
	var new_packet: JoinRoomClass = JoinRoomClass.new(PACKET_TYPE.JOIN_ROOM)
	new_packet.decode(data)
	return new_packet

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	
	room_id = data.decode_u8(1)
	room_port = data.decode_u16(2)
	var remote_count = data.decode_u8(4)
	
	remote_ids.clear()
	var current_offset = 5
	for i in range(remote_count):
		var r_id = data.decode_u8(current_offset)
		remote_ids.append(r_id)
		current_offset += 1 
	
	var string_data = data.slice(current_offset) 
	host_ip = string_data.get_string_from_utf8()

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = PackedByteArray()
	
	var ip_bytes = host_ip.to_utf8_buffer()
	var ids_count = remote_ids.size()
	
	var total_size = FIXED_HEADER_SIZE + ids_count + ip_bytes.size()
	new_array.resize(total_size)
	new_array.encode_u8(0, packet_type) 
	new_array.encode_u8(1, room_id)
	new_array.encode_u16(2, room_port)
	new_array.encode_u8(4, ids_count)
	var current_offset = 5
	for id in remote_ids:
		new_array.encode_u8(current_offset, id)
		current_offset += 1
	for i in range(ip_bytes.size()):
		new_array[current_offset + i] = ip_bytes[i]
		
	return new_array
