class_name MinigameGradeResultPkt extends InGameTypeClass

# Host -> broadcast in-game (receiver filters by team).
# Camada 2 do entrega de nota. Vem do host depois que ele recebeu um
# MinigameGradePkt do server. Players da dupla com team_index = my_team
# trocam o "Aguarde o professor..." pela nota + comentario.
#
# Layout: [u8 type][u8 team_index][f32 grade][u16 comment_size][comment_bytes]

var team_index: int
var grade: float
var comment: String

static func create(_team_index: int, _grade: float, _comment: String) -> MinigameGradeResultPkt:
	var pkt: MinigameGradeResultPkt = MinigameGradeResultPkt.new(PACKET_TYPE.MINIGAME_GRADE_RESULT, ENetPacketPeer.FLAG_RELIABLE)
	pkt.team_index = _team_index
	pkt.grade = _grade
	pkt.comment = _comment
	return pkt

static func create_from_data(data: PackedByteArray) -> MinigameGradeResultPkt:
	var pkt: MinigameGradeResultPkt = MinigameGradeResultPkt.new(PACKET_TYPE.MINIGAME_GRADE_RESULT, ENetPacketPeer.FLAG_RELIABLE)
	pkt.decode(data)
	return pkt

func encode() -> PackedByteArray:
	var buf: PackedByteArray = super.encode()
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
	team_index = data.decode_u8(1)
	grade = data.decode_float(2)
	var comment_size: int = data.decode_u16(6)
	comment = data.slice(8, 8 + comment_size).get_string_from_utf8()
