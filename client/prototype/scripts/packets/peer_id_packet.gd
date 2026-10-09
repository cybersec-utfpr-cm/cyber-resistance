class_name PeerId extends PacketTypeClass

var id: int

static func create(client_id: int) -> PeerId:
	var packet: PeerId = PeerId.new(PacketTypeClass.PACKET_TYPE.PEER_ID, ENetPacketPeer.FLAG_RELIABLE)
	packet.id = client_id
	return packet

static func create_from_data(data: PackedByteArray) -> PeerId:
	var new_class: PeerId = PeerId.new(PacketTypeClass.PACKET_TYPE.PEER_ID, ENetPacketPeer.FLAG_RELIABLE)
	new_class.decode(data)
	return new_class

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = super.encode()
	new_array.resize(2)
	new_array.encode_u8(1, id)
	return new_array

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	id = data.decode_u8(1)
