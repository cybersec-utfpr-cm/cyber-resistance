extends Node

signal from_client_packet(peer: ENetPacketPeer, data: PackedByteArray)

var server_connection: ENetConnection
# range(255,-1,-1) + pop_back() devolve ids em ordem crescente (0,1,2,3...).
# O cliente usa essa ordem para decidir papel: my_id % 4 == 0 vira host de sala.
var avaliable_peer_ids: Array = range(255, -1, -1)
var peers_connected: Dictionary[int, ENetPacketPeer]

# [TCC eval] log periodico de RTT/perda em user://enet_stats_server_<unix>.csv.
const _STATS_INTERVAL_S: float = 1.0
var _stats_file: FileAccess
var _stats_accum: float = 0.0

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

func _process(delta: float) -> void:
	if server_connection == null:
		return
	handle_events()
	_stats_tick(delta, peers_connected.values())

func handle_events() -> void:
	var packet_event: Array = server_connection.service()
	var event_type: ENetConnection.EventType = packet_event[0]
	while event_type != ENetConnection.EventType.EVENT_NONE:
		var peer_sender: ENetPacketPeer = packet_event[1]
		match event_type:
			ENetConnection.EVENT_ERROR:
				print("Packet received error")
			ENetConnection.EVENT_CONNECT:
				peer_connected(peer_sender)
				# Debug timeout — mesmo valor de antes para testes longos.
				peer_sender.set_timeout(0, 10000, 10000)
			ENetConnection.EVENT_DISCONNECT:
				peer_disconnected(peer_sender)
			ENetConnection.EVENT_RECEIVE:
				from_client_packet.emit(peer_sender, peer_sender.get_packet())
		packet_event = server_connection.service()
		event_type = packet_event[0]

func start_server(ip_address: String, port: int = 42069) -> bool:
	server_connection = ENetConnection.new()
	var error: Error = server_connection.create_host_bound(ip_address, port)
	if error != OK:
		printerr("ERRO FATAL AO CRIAR HOST: ", error)
		server_connection = null
		return false
	print("Server started at ", ip_address, ":", port)
	_stats_open("server")
	return true

func peer_connected(peer: ENetPacketPeer) -> void:
	var peer_id: int = avaliable_peer_ids.pop_back()
	peer.set_meta("id", peer_id)
	peers_connected[peer_id] = peer
	# PEER_ID deve sair ANTES do REFRESH: o cliente decide se vai ser host
	# (my_id % 4 == 0) dentro do callback de refresh, então my_id precisa estar setado.
	PeerId.create(peer_id).send(peer)
	ServerPacketHandler.send_refresh(peer)
	print("(Server network) Peer: ", peer_id, " successfully connected")

func peer_disconnected(peer: ENetPacketPeer) -> void:
	var peer_id: int = peer.get_meta("id")
	avaliable_peer_ids.push_back(peer_id)
	peers_connected.erase(peer_id)
	print("Peer: ", peer_id, " successfully disconnected")

# [TCC eval] abre arquivo de estatisticas para o papel atual.
# Caminho real em OS.get_user_data_dir(); o print mostra o absoluto.
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
