extends Node

signal from_server_packet(data: PackedByteArray)
signal on_peer_connected
signal on_connection_error

var server_connection: ENetConnection
var server_peer: ENetPacketPeer
var server_ip: String = ""

# [TCC eval] log periodico de RTT/perda em user://enet_stats_client_lobby_<unix>.csv.
const _STATS_INTERVAL_S: float = 1.0
var _stats_file: FileAccess
var _stats_accum: float = 0.0

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

func _process(delta: float) -> void:
	if server_connection == null:
		return
	handle_events()
	if server_peer != null:
		_stats_tick(delta, [server_peer])

func handle_events() -> void:
	var packet_event: Array = server_connection.service()
	var event_type: ENetConnection.EventType = packet_event[0]
	while event_type != ENetConnection.EventType.EVENT_NONE:
		var peer_sender: ENetPacketPeer = packet_event[1]
		match event_type:
			ENetConnection.EVENT_ERROR:
				print("Packet received error")
			ENetConnection.EVENT_CONNECT:
				client_connection()
				# Debug timeout — mesmo valor de antes para testes longos.
				peer_sender.set_timeout(0, 10000, 10000)
			ENetConnection.EVENT_DISCONNECT:
				client_disconnection()
				return
			ENetConnection.EVENT_RECEIVE:
				from_server_packet.emit(peer_sender.get_packet())
		packet_event = server_connection.service()
		event_type = packet_event[0]

func start_client(ip_address: String, port: int) -> void:
	server_connection = ENetConnection.new()
	var error: Error = server_connection.create_host(1)
	if error:
		print("Host creation erro: ", error)
		return
	server_ip = ip_address
	server_peer = server_connection.connect_to_host(ip_address, port)
	# Sem isso, o handshake desiste só apos ~30s (default ENet) quando o IP esta errado.
	server_peer.set_timeout(0, 5000, 10000)

# Desconexao explicita (botao do lobby / voltar ao menu): diferente de
# client_disconnection, nao troca de cena nem emite erro.
func stop_client() -> void:
	if server_connection == null:
		return
	server_connection.destroy()
	server_peer = null
	server_connection = null
	_stats_file = null

func client_connection() -> void:
	_stats_open("client_lobby")
	on_peer_connected.emit()

# Chamado tanto em disconnect explícito quanto no timeout (caminho de IP errado).
# Volta sempre ao menu para evitar UI travada em "Conectando..." indefinidamente.
func client_disconnection() -> void:
	print("Connection ended (state: %d)" % server_peer.get_state())
	server_peer = null
	server_connection = null
	_stats_file = null
	get_tree().change_scene_to_file("res://prototype/scenes/menu/mainMenu.tscn")
	on_connection_error.emit()

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
