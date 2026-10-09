class_name StartRoomClass extends PacketTypeClass

var requester: int
var room: int

static func create(room_id: int, requester_id: int) -> StartRoomClass:
	var new_packet: StartRoomClass = StartRoomClass.new(PacketTypeClass.PACKET_TYPE.START_ROOM, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.requester = requester_id
	new_packet.room = room_id
	return new_packet

static func create_from_data(data: PackedByteArray) -> StartRoomClass:
	var new_packet: StartRoomClass = StartRoomClass.new(PacketTypeClass.PACKET_TYPE.START_ROOM, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	requester = data.decode_u8(1)
	room = data.decode_u8(2)

func encode() -> PackedByteArray:
	var new_packed: PackedByteArray = super.encode()
	new_packed.resize(3)
	new_packed.encode_u8(1, requester)
	new_packed.encode_u8(2, room)
	return new_packed
