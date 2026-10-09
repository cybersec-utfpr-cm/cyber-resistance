class_name RoomRequestClass extends PacketTypeClass

var id: int

static func create(client_id: int) -> RoomRequestClass:
	var new_packet: RoomRequestClass = RoomRequestClass.new(PacketTypeClass.PACKET_TYPE.ROOM_REQUEST)
	new_packet.id = client_id
	return new_packet

static func create_from_data(data: PackedByteArray) -> RoomRequestClass:
	var new_packet: RoomRequestClass = RoomRequestClass.new(PacketTypeClass.PACKET_TYPE.ROOM_REQUEST)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = super.encode()
	new_array.resize(2)
	new_array.encode_u8(1, id)
	return new_array

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	id = data.decode_u8(1)
