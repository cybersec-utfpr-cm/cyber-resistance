class_name MinigameSubmissionPkt extends PacketTypeClass

# Host -> servidor central.
# Disparado quando AS DUAS duplas da sala finalizam as perguntas. Carrega as
# respostas e os tempos de cada dupla para o professor avaliar. Sem nota — só
# texto puro + tempos. A nota volta depois via outro pacote (passo futuro).
#
# Layout do payload (tudo após o byte 0 com o packet_type):
#   [u8 room_id]
#   [u8 num_teams]                  # sempre 2 hoje
#   por dupla:
#     [u8 num_members][u8 id]*N     # ids dos integrantes (2)
#     [u32 total_msec]              # tempo total da dupla
#     [u8 num_answers]              # sempre = num_questions hoje
#     por resposta:
#       [u32 submission_msec]
#       [u8 answer_size][answer_bytes...]

var room_id: int
# Cada entrada: { members: Array[int], total_msec: int,
#                 answers: Array[String], submission_msecs: Array[int] }
var teams: Array = []

static func create(_room_id: int, _teams: Array) -> MinigameSubmissionPkt:
	var new_packet: MinigameSubmissionPkt = MinigameSubmissionPkt.new(PACKET_TYPE.MINIGAME_SUBMISSION, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.room_id = _room_id
	new_packet.teams = _teams
	return new_packet

static func create_from_data(data: PackedByteArray) -> MinigameSubmissionPkt:
	var new_packet: MinigameSubmissionPkt = MinigameSubmissionPkt.new(PACKET_TYPE.MINIGAME_SUBMISSION, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var buf: PackedByteArray = super.encode()
	buf.append(room_id)
	buf.append(teams.size())
	for team in teams:
		var members: Array = team["members"]
		buf.append(members.size())
		for m in members:
			buf.append(int(m))
		var total_buf: PackedByteArray = PackedByteArray()
		total_buf.resize(4)
		total_buf.encode_u32(0, int(team["total_msec"]))
		buf.append_array(total_buf)
		var answers: Array = team["answers"]
		var submission_msecs: Array = team["submission_msecs"]
		buf.append(answers.size())
		for i in answers.size():
			var ms_buf: PackedByteArray = PackedByteArray()
			ms_buf.resize(4)
			ms_buf.encode_u32(0, int(submission_msecs[i]))
			buf.append_array(ms_buf)
			var answer_bytes: PackedByteArray = String(answers[i]).to_utf8_buffer()
			buf.append(answer_bytes.size())
			buf.append_array(answer_bytes)
	return buf

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	var offset: int = 1
	room_id = data.decode_u8(offset)
	offset += 1
	var num_teams: int = data.decode_u8(offset)
	offset += 1
	teams = []
	for _t in range(num_teams):
		var num_members: int = data.decode_u8(offset)
		offset += 1
		var members: Array[int] = []
		for _m in range(num_members):
			members.append(data.decode_u8(offset))
			offset += 1
		var total_msec: int = data.decode_u32(offset)
		offset += 4
		var num_answers: int = data.decode_u8(offset)
		offset += 1
		var answers: Array[String] = []
		var submission_msecs: Array[int] = []
		for _a in range(num_answers):
			submission_msecs.append(data.decode_u32(offset))
			offset += 4
			var answer_size: int = data.decode_u8(offset)
			offset += 1
			answers.append(data.slice(offset, offset + answer_size).get_string_from_utf8())
			offset += answer_size
		teams.append({
			"members": members,
			"total_msec": total_msec,
			"answers": answers,
			"submission_msecs": submission_msecs
		})
