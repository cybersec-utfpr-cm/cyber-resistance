class_name MinigameSelectPkt extends PacketTypeClass

# Professor -> server -> host da sala (lobby).
# Define qual tarefa/atividade (activity_id) deve ser aplicada.
# room_id = 255 aplica para todas as salas.
# Layout: [u8 type = 85][u8 room_id][u8 activity_id_size][activity_id_bytes]

const ALL_ROOMS: int = 255

var room_id: int = ALL_ROOMS
var activity_id: String = ""

static func create(_room_id: int, _activity_id: String) -> MinigameSelectPkt:
	var pkt: MinigameSelectPkt = MinigameSelectPkt.new(PACKET_TYPE.MINIGAME_SELECT, ENetPacketPeer.FLAG_RELIABLE)
	pkt.room_id = ALL_ROOMS if _room_id < 0 else _room_id
	pkt.activity_id = _activity_id
	return pkt

static func create_from_data(data: PackedByteArray) -> MinigameSelectPkt:
	var pkt: MinigameSelectPkt = MinigameSelectPkt.new(PACKET_TYPE.MINIGAME_SELECT, ENetPacketPeer.FLAG_RELIABLE)
	pkt.decode(data)
	return pkt

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	data.append(room_id)
	var id_bytes: PackedByteArray = activity_id.to_utf8_buffer()
	data.append(id_bytes.size())
	data.append_array(id_bytes)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	room_id = data.decode_u8(1)
	var id_size: int = data.decode_u8(2)
	activity_id = data.slice(3, 3 + id_size).get_string_from_utf8()
