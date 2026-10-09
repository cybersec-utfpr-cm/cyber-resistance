extends Node

# Layer 2 (in-game) - ENetConnection separada do lobby (Layer 1, ProtNetworkHandler).
# As duas camadas são totalmente independentes: enums de pacote, autoload,
# signals e base classes não se cruzam. Mantém o gameplay fora do servidor central.

# Host player signals
signal game_scripts_setup(is_host: bool)

# Server signals
signal from_host_packet(data: PackedByteArray)

# Client signals
signal from_player_packet(peer: ENetPacketPeer, data: PackedByteArray)

# Client Server variables
var host_connection: ENetConnection

# Server variables
var is_host: bool
var avaliable_player_ids: Array = range(3, -1, -1)

# Client variables
var host_peer: ENetPacketPeer
var is_connected_to_host: bool = false

# [TCC eval] log periodico de RTT/perda em user://enet_stats_game_<role>_<unix>.csv.
const _STATS_INTERVAL_S: float = 1.0
var _stats_file: FileAccess
var _stats_accum: float = 0.0

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

func _process(delta: float) -> void:
	if host_connection == null: return
	handle_events()
	if is_host:
		_stats_tick(delta, host_connection.get_peers())
	elif host_peer != null:
		_stats_tick(delta, [host_peer])

func handle_events() -> void:
	var packet_event: Array = host_connection.service()
	var event_type: ENetConnection.EventType = packet_event[0]
	while event_type != ENetConnection.EventType.EVENT_NONE:
		var peer_sender: ENetPacketPeer = packet_event[1]
		match event_type:
			ENetConnection.EVENT_ERROR:
				print("Packet received error")
			ENetConnection.EVENT_CONNECT:
				if is_host:
					player_connected(peer_sender)
					#Debug timeout
					peer_sender.set_timeout(0, 10000, 10000)
				else:
					player_connection(peer_sender)
					#Debug timeout
					peer_sender.set_timeout(0, 10000, 10000)
			ENetConnection.EVENT_DISCONNECT:
				if is_host:
					peer_disconnected(peer_sender)
				else:
					host_disconnection()
					return
			ENetConnection.EVENT_RECEIVE:
				if is_host:
					#print("Player packet",peer_sender.get_packet())
					from_player_packet.emit(peer_sender, peer_sender.get_packet())
				else:
					#print("Host packet",peer_sender.get_packet())
					from_host_packet.emit(peer_sender.get_packet())
					
		packet_event = host_connection.service()
		event_type = packet_event[0]

# create_host_bound: aceita conexões de entrada na porta sorteada.
# is_host=true muda a rota dos eventos de receive (from_player_packet).
func start_host(ip_address: String = "127.0.0.1", port: int = 42069) -> void:
	host_connection = ENetConnection.new()
	var error: Error = host_connection.create_host_bound(ip_address, port)
	if error:
		print("(Game network) Host bound creation error: ", error)
		return
	else:
		print("(Game network) Host started!")
		is_host = true
		is_connected_to_host = false
		PlayerHostPacketHandler.setup_packet_handler()
		_stats_open("game_host")

func player_connected(_peer: ENetPacketPeer) -> void:
	print("(Game network) new player connected")
	var current_scene_path: String = get_tree().current_scene.scene_file_path
	if not current_scene_path.is_empty():
		SceneSyncPacket.create(ClientPacketHandler.my_id, current_scene_path).send(_peer)

func peer_disconnected(peer: ENetPacketPeer) -> void:
	var player_id: int = peer.get_meta("id")
	avaliable_player_ids.push_back(player_id)
	
	print("(Game network) Peer: ", player_id, " successfully disconnected")

# create_host(1): só um peer outbound (o host da sala).
# is_host=false roteia receives via from_host_packet.
func start_player(ip_address: String, port: int) -> void:
	var client_connection = ENetConnection.new()
	var error: Error = client_connection.create_host(1)
	if error:
		print("(Game network) Host creation erro: ", error)
		return
	host_connection = client_connection
	is_host = false
	is_connected_to_host = false
	host_peer = client_connection.connect_to_host(ip_address, port)
	PlayerHostPacketHandler.setup_packet_handler()
	_stats_open("game_player")
	game_scripts_setup.emit(is_host)

func player_connection(peer: ENetPacketPeer) -> void:
	host_peer = peer
	is_connected_to_host = true
	print("(Game network) connected to host")
	return

func host_disconnection() -> void:
	print("(Game network) Host disconnected")
	is_connected_to_host = false
	host_peer = null
	host_connection = null

func can_send_to_host() -> bool:
	return !is_host and is_connected_to_host and host_peer != null

func cleanup_connection() -> void:
	# Chamado quando o player sai do room. Setar null libera a conexão ENet
	# para GC; sem isso o socket fica pendurado até timeout dos peers.
	is_connected_to_host = false
	is_host = false
	host_peer = null
	host_connection = null
	_stats_file = null

# [TCC eval] abre arquivo de estatisticas para o papel atual.
func _stats_open(role: String) -> void:
	var path := "user://enet_stats_%s_%d.csv" % [role, int(Time.get_unix_time_from_system())]
	_stats_file = FileAccess.open(path, FileAccess.WRITE)
	if _stats_file == null:
		push_error("[stats] open failed: %s" % path)
		return
	_stats_file.store_line("timestamp,peer,rtt_ms,last_rtt_ms,packet_loss_pct")
	print("[stats] gravando em ", ProjectSettings.globalize_path(path))

# [TCC eval] amostra a cada _STATS_INTERVAL_S segundos.
# PEER_PACKET_LOSS escala 0..65535 (=100%); normalizamos para porcentagem.
func _stats_tick(delta: float, peers: Array) -> void:
	if _stats_file == null:
		return
	_stats_accum += delta
	if _stats_accum < _STATS_INTERVAL_S:
		return
	_stats_accum = 0.0
	var ts := int(Time.get_unix_time_from_system())
	for peer in peers:
		if peer == null:
			continue
		if peer.get_state() != ENetPacketPeer.STATE_CONNECTED:
			continue
		var label := str(peer.get_meta("id")) if peer.has_meta("id") else "peer"
		# get_statistic devolve Variant; tipagem explicita evita "cannot infer".
		var rtt: float = peer.get_statistic(ENetPacketPeer.PEER_ROUND_TRIP_TIME)
		var last_rtt: float = peer.get_statistic(ENetPacketPeer.PEER_LAST_ROUND_TRIP_TIME)
		var loss: float = peer.get_statistic(ENetPacketPeer.PEER_PACKET_LOSS)
		_stats_file.store_line("%d,%s,%.1f,%.1f,%.4f" % [
			ts, label, rtt, last_rtt, (loss / 65535.0) * 100.0
		])
	_stats_file.flush()
