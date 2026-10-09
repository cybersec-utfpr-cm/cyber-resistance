class_name MinigameGradePkt extends PacketTypeClass

# Professor -> server -> host da sala (lobby).
# Nota + comentario de uma dupla especifica. Professor preenche na UI, envia
# ao server; server valida que veio do professor registrado e encaminha o
# MESMO pacote ao host da sala correspondente. O host entao traduz em
# MinigameGradeResultPkt (in-game) e faz broadcast pros players.
#
# Layout: [u8 type][u8 room_id][u8 team_index][f32 grade][u16 comment_size][comment_bytes]

var room_id: int
var team_index: int
var grade: float
var comment: String

static func create(_room_id: int, _team_index: int, _grade: float, _comment: String) -> MinigameGradePkt:
	var pkt: MinigameGradePkt = MinigameGradePkt.new(PACKET_TYPE.MINIGAME_GRADE, ENetPacketPeer.FLAG_RELIABLE)
	pkt.room_id = _room_id
	pkt.team_index = _team_index
	pkt.grade = _grade
	pkt.comment = _comment
	return pkt

static func create_from_data(data: PackedByteArray) -> MinigameGradePkt:
	var pkt: MinigameGradePkt = MinigameGradePkt.new(PACKET_TYPE.MINIGAME_GRADE, ENetPacketPeer.FLAG_RELIABLE)
	pkt.decode(data)
	return pkt

func encode() -> PackedByteArray:
	var buf: PackedByteArray = super.encode()
	buf.append(room_id)
	buf.append(team_index)
	var grade_buf: PackedByteArray = PackedByteArray()
	grade_buf.resize(4)
	grade_buf.encode_float(0, grade)
	buf.append_array(grade_buf)
	var comment_bytes: PackedByteArray = comment.to_utf8_buffer()
	var size_buf: PackedByteArray = PackedByteArray()
	size_buf.resize(2)
	size_buf.encode_u16(0, comment_bytes.size())
	buf.append_array(size_buf)
	buf.append_array(comment_bytes)
	return buf

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	room_id = data.decode_u8(1)
	team_index = data.decode_u8(2)
	grade = data.decode_float(3)
	var comment_size: int = data.decode_u16(7)
	comment = data.slice(9, 9 + comment_size).get_string_from_utf8()
