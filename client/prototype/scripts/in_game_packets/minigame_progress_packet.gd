class_name MinigameProgressPkt extends InGameTypeClass

# Host -> broadcast (receiver filters by team).
# Comunica o estado da dupla após cada resposta enviada: qual é a próxima
# pergunta, quem assume o papel de QUIZ a seguir (DOC e QUIZ trocam a cada
# submit) e se a dupla concluiu todas as perguntas (aguardando nota do prof).

var team: int
var question_idx: int
var quiz_player_id: int
var finished: int

static func create(_team: int, _question_idx: int, _quiz_player_id: int, _finished: bool) -> MinigameProgressPkt:
	var new_packet: MinigameProgressPkt = MinigameProgressPkt.new(PACKET_TYPE.MINIGAME_PROGRESS, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.team = _team
	new_packet.question_idx = _question_idx
	new_packet.quiz_player_id = _quiz_player_id
	new_packet.finished = 1 if _finished else 0
	return new_packet

static func create_from_data(data: PackedByteArray) -> MinigameProgressPkt:
	var new_packet: MinigameProgressPkt = MinigameProgressPkt.new(PACKET_TYPE.MINIGAME_PROGRESS, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	data.append(team)
	data.append(question_idx)
	data.append(quiz_player_id)
	data.append(finished)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	team = data.decode_u8(1)
	question_idx = data.decode_u8(2)
	quiz_player_id = data.decode_u8(3)
	finished = data.decode_u8(4)
