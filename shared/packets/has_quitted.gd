class_name HasQuittedPkt extends PacketTypeClass

var remote_ids: Array[int] = []

const MAX_IDS = 4
const HEADER_SIZE = 2

static func create(_remote_ids: Array[int]) -> HasQuittedPkt:
	var packet = HasQuittedPkt.new(PACKET_TYPE.HAS_QUITTED)
	packet.remote_ids = _remote_ids
	return packet

static func create_from_data(data: PackedByteArray) -> HasQuittedPkt:
	var packet = HasQuittedPkt.new(PACKET_TYPE.HAS_QUITTED)
	packet.decode(data)
	return packet

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	var remote_count = data.decode_u8(1)

	remote_ids.clear()
	var actual_count = min(remote_count, MAX_IDS)

	for i in range(actual_count):
		remote_ids.append(data.decode_u8(2 + i))

func encode() -> PackedByteArray:
	var ids_count = min(remote_ids.size(), MAX_IDS)
	var new_array: PackedByteArray = super.encode()
	new_array.resize(HEADER_SIZE + ids_count)
	new_array.encode_u8(1, ids_count)
	for i in range(ids_count):
		new_array.encode_u8(2 + i, remote_ids[i])

	return new_array
