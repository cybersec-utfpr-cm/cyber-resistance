class_name MinigameAnswerPkt extends InGameTypeClass

var team: int
var question_idx: int
var answer: String

static func create(_team: int, _question_idx: int, _answer: String) -> MinigameAnswerPkt:
	var new_packet: MinigameAnswerPkt = MinigameAnswerPkt.new(PACKET_TYPE.MINIGAME_ANSWER, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.team = _team
	new_packet.question_idx = _question_idx
	new_packet.answer = _answer
	return new_packet

static func create_from_data(data: PackedByteArray) -> MinigameAnswerPkt:
	var new_packet: MinigameAnswerPkt = MinigameAnswerPkt.new(PACKET_TYPE.MINIGAME_ANSWER, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	data.append(team)
	data.append(question_idx)
	var answer_data: PackedByteArray = answer.to_utf8_buffer()
	data.append(answer_data.size())
	data.append_array(answer_data)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	team = data.decode_u8(1)
	question_idx = data.decode_u8(2)
	var answer_size: int = data.decode_u8(3)
	answer = data.slice(4, 4 + answer_size).get_string_from_utf8()
