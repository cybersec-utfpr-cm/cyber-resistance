extends Node

signal created_room(room_id: int)
signal room_refresh(summaries: Array[RoomSummary])
signal join_room(room_id: int)
signal quit_room()
signal spawn_player_signal(player_id: int)
signal player_scene_changed(player_id: int, scene_path: String)
signal minigame_assigned(packet: MinigameAssignPkt)
# Emitido APENAS no host (loopback local) quando o grade chega via lobby.
# Players nao-host recebem direto via PlayerHostPacketHandler.host_minigame_grade_signal.
signal host_minigame_grade(team_index: int, grade: float, comment: String)

var my_ip: String
var room_port: int

var my_id: int = -1
var temporary_player_name: String = "player"
var current_room_id: int
var spawned_ids: Array[int]
var players_scenes: Dictionary[int, String] = {}
# MinigameAssignPkt chega no broadcast logo após o SceneForcePacket, mas
# change_scene_to_file é diferido — quando o pacote chega, a cena nova ainda
# não está montada. Bufferizamos aqui (autoload) para o MinigameSession
# consumir no seu _ready().
var pending_minigame_assign: MinigameAssignPkt = null
# -1 enquanto fora do minigame. Quando setado, o chat filtra mensagens fora-do-time
# e colore os nomes por dupla.
var minigame_team: int = -1
var minigame_team_members: Array[int] = []

func _ready() -> void:
	ProtNetworkHandler.from_server_packet.connect(packet_handler)
	PlayerHostPacketHandler.host_change_scene_signal.connect(on_scene_sync_received)
	PlayerHostPacketHandler.player_change_scene_signal.connect(on_player_scene_received)
	PlayerHostPacketHandler.host_force_scene_signal.connect(on_force_scene_received)
	PlayerHostPacketHandler.host_minigame_assign_signal.connect(on_minigame_assign_received)

func packet_handler(data: PackedByteArray) -> void:
	var packet_type: int = int(data.decode_u8(0))
	match packet_type:
		PacketTypeClass.PACKET_TYPE.PEER_ID:
			var peer_id: PeerId = PeerId.create_from_data(data)
			my_id = peer_id.id
			temporary_player_name = "player_%d" % my_id
		PacketTypeClass.PACKET_TYPE.START_ROOM:
			start_room(data)
		PacketTypeClass.PACKET_TYPE.JOIN_ROOM:
			join_manager(data)
		PacketTypeClass.PACKET_TYPE.QUIT_ROOM:
			current_room_id = -1
			GamePacketHandler.cleanup_connection()
			spawned_ids.clear()
			players_scenes.clear()
			pending_minigame_assign = null
			minigame_team = -1
			minigame_team_members.clear()
			quit_room.emit()
		PacketTypeClass.PACKET_TYPE.REFRESH:
			var refresh: RefreshClass = RefreshClass.create_from_data(data)
			room_refresh.emit(refresh.summaries)
		PacketTypeClass.PACKET_TYPE.HAS_JOINED:
			var has_joined: HasJoinedPkt = HasJoinedPkt.create_from_data(data)
			sync_spawns(has_joined.remote_ids)
		PacketTypeClass.PACKET_TYPE.HAS_QUITTED:
			var has_quited: HasQuittedPkt = HasQuittedPkt.create_from_data(data)
			sync_spawns(has_quited.remote_ids)
		PacketTypeClass.PACKET_TYPE.MINIGAME_GRADE:
			# So o host recebe isso. Traduz em pacote in-game e broadcast.
			if not GamePacketHandler.is_host:
				return
			var grade_pkt: MinigameGradePkt = MinigameGradePkt.create_from_data(data)
			MinigameGradeResultPkt.create(grade_pkt.team_index, grade_pkt.grade, grade_pkt.comment).broadcast(GamePacketHandler.host_connection)
			# Broadcast nao volta ao remetente; se o host pertence a dupla, aplica local.
			host_minigame_grade.emit(grade_pkt.team_index, grade_pkt.grade, grade_pkt.comment)

func join_manager(data: PackedByteArray) -> void:
	var join_packet: JoinRoomClass = JoinRoomClass.create_from_data(data)
	current_room_id = join_packet.room_id
	GamePacketHandler.start_player(join_packet.host_ip, join_packet.room_port)
	join_room.emit(join_packet.room_id)
	sync_spawns(join_packet.remote_ids)
	#print("id and remote ids: ", my_id, join_packet.remote_ids)

func start_room(data: PackedByteArray) -> void:
	var room_packet: StartRoomClass = StartRoomClass.create_from_data(data)

	current_room_id = room_packet.room
	created_room.emit(room_packet.room)
	if my_ip.is_empty():
		my_ip = get_ipv4()
	room_port = get_random_port()
	
	RoomInfoClass.create(ClientPacketHandler.my_id, current_room_id, room_port, my_ip, temporary_player_name).send(ProtNetworkHandler.server_peer)
	GamePacketHandler.start_host(my_ip, room_port)
	spawn_player(my_id)
	print("(Client handler) room id: ", room_packet.room)

func get_random_port() -> int:
	var temp_udp = PacketPeerUDP.new()
	if temp_udp.bind(0) == OK:
		var port = temp_udp.get_local_port()
		temp_udp.close()
		return port
	return 0

func get_ipv4() -> String:
	var ip_list: Array = IP.get_local_addresses()
	var candidates: Array = []
	for ip in ip_list:
		if ip.count(".") == 3 and (ip.begins_with("192.168.") or ip.begins_with("10.") or ip.begins_with("172.")):
			candidates.append(ip)
	var server_prefix: String = _ipv4_prefix(ProtNetworkHandler.server_ip)
	if server_prefix != "":
		for ip in candidates:
			if _ipv4_prefix(ip) == server_prefix:
				return ip
	if candidates.size() > 0:
		return candidates[0]
	return ""

func _ipv4_prefix(ip: String) -> String:
	var parts: PackedStringArray = ip.split(".")
	if parts.size() != 4:
		return ""
	return "%s.%s.%s" % [parts[0], parts[1], parts[2]]

func sync_spawns(others_id: Array[int]) -> void:
	for spawned_id in spawned_ids.duplicate():
		if spawned_id not in others_id:
			despawn_player(spawned_id)

	for i in others_id:
		if i not in spawned_ids:
			spawn_player(i)

func despawn_player(spawn_id: int) -> void:
	for player_node in get_tree().get_nodes_in_group("players"):
		if player_node.owner_id == spawn_id:
			player_node.queue_free()
			spawned_ids.erase(spawn_id)
			return
	spawned_ids.erase(spawn_id)

func spawn_player(spawn_id: int) -> void:
	spawn_player_signal.emit(spawn_id)
	spawned_ids.append(spawn_id)

func on_scene_sync_received(data: PackedByteArray) -> void:
	var packet: SceneSyncPacket = SceneSyncPacket.create_from_data(data)
	if packet.peer_id == my_id:
		return
	if not spawned_ids.has(packet.peer_id):
		return
	players_scenes[packet.peer_id] = packet.scene_path
	player_scene_changed.emit(packet.peer_id, packet.scene_path)

func on_player_scene_received(_peer: ENetPacketPeer, data: PackedByteArray) -> void:
	var packet: SceneSyncPacket = SceneSyncPacket.create_from_data(data)
	players_scenes[packet.peer_id] = packet.scene_path
	SceneSyncPacket.create(packet.peer_id, packet.scene_path).broadcast(GamePacketHandler.host_connection)
	player_scene_changed.emit(packet.peer_id, packet.scene_path)

func on_force_scene_received(data: PackedByteArray) -> void:
	var packet: SceneForcePacket = SceneForcePacket.create_from_data(data)
	GameManager.goto_scene(packet.scene_path)

func on_minigame_assign_received(data: PackedByteArray) -> void:
	var packet: MinigameAssignPkt = MinigameAssignPkt.create_from_data(data)
	if packet.target_id != my_id:
		return
	pending_minigame_assign = packet
	minigame_assigned.emit(packet)
