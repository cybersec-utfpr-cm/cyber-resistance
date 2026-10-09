class_name QuitRequestClass extends PacketTypeClass

var room_id: int

static func create(room_id_: int) -> QuitRequestClass:
	var new_packet: QuitRequestClass = QuitRequestClass.new(PACKET_TYPE.QUIT_ROOM_REQUEST)
	new_packet.room_id = room_id_
	return new_packet

static func create_from_data(data: PackedByteArray) -> QuitRequestClass:
	var new_packet: QuitRequestClass = QuitRequestClass.new(PACKET_TYPE.QUIT_ROOM_REQUEST)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = super.encode()
	new_array.resize(2)
	new_array.encode_u8(1, room_id)
	return new_array

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	room_id = data.decode_u8(1)
