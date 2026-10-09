extends Node

# player signal
signal player_movement_signal(data: PackedByteArray)
signal player_text_signal(data: PackedByteArray)
signal player_change_scene_signal(peer: ENetPacketPeer, data: PackedByteArray)
signal player_minigame_answer_signal(data: PackedByteArray)

# host signal
signal host_movement_signal(data: PackedByteArray)
signal host_text_signal(data: PackedByteArray)
signal host_change_scene_signal(data: PackedByteArray)
signal host_force_scene_signal(data: PackedByteArray)
signal host_minigame_assign_signal(data: PackedByteArray)
signal host_minigame_progress_signal(data: PackedByteArray)
signal host_minigame_grade_signal(data: PackedByteArray)

var is_host: bool

# Conecta APENAS um lado por instância: host OU player, nunca os dois.
# Se conectasse ambos, cada pacote dispararia o handler duas vezes
# (host_packet_handler entende SCENE_FORCE_PACKET, player_packet_handler não).
func setup_packet_handler() -> void:
	is_host = GamePacketHandler.is_host
	if is_host:
		GamePacketHandler.from_player_packet.connect(player_packet_handler)
	else :
		GamePacketHandler.from_host_packet.connect(host_packet_handler)

func player_packet_handler(_peer: ENetPacketPeer, data: PackedByteArray) -> void:
	var packet_type = data.decode_u8(0)
	match packet_type:
		InGameTypeClass.PACKET_TYPE.PLAYER_PACKET:
			player_movement_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.TEXT_PACKET:
			player_text_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.SCENE_SYNC_PACKET:
			player_change_scene_signal.emit(_peer, data)
		InGameTypeClass.PACKET_TYPE.MINIGAME_ANSWER:
			player_minigame_answer_signal.emit(data)

func host_packet_handler(data: PackedByteArray) -> void:
	var packet_type = data.decode_u8(0)
	match packet_type:
		InGameTypeClass.PACKET_TYPE.PLAYER_PACKET:
			host_movement_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.TEXT_PACKET:
			host_text_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.SCENE_SYNC_PACKET:
			host_change_scene_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.SCENE_FORCE_PACKET:
			host_force_scene_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.MINIGAME_ASSIGN:
			host_minigame_assign_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.MINIGAME_PROGRESS:
			host_minigame_progress_signal.emit(data)
		InGameTypeClass.PACKET_TYPE.MINIGAME_GRADE_RESULT:
			host_minigame_grade_signal.emit(data)
