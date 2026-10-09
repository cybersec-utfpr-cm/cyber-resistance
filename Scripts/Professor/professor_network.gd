extends Node

# Cliente ENet do console do professor.
# Conecta ao server central (Scripts/Multiplayer/network_handler.gd), se
# registra como professor com PROFESSOR_HELLO e converte os pacotes recebidos
# em sinais para a UI. Roda como no da cena do professor, sem autoload.

signal connected_to_server
signal disconnected_from_server
signal peer_id_received(peer_id: int)
signal rooms_received(refresh: RefreshClass)
signal submission_received(submission: MinigameSubmissionPkt)
signal status_changed(text: String)

var _connection: ENetConnection = null
var _server_peer: ENetPacketPeer = null

func is_connected_to_server() -> bool:
	return _server_peer != null and _server_peer.get_state() == ENetPacketPeer.STATE_CONNECTED

func connect_to_server(ip: String, port: int) -> Error:
	disconnect_from_server()
	_connection = ENetConnection.new()
	# O servidor tambem usa a API de baixo nivel: host proprio + service().
	var error: Error = _connection.create_host()
	if error != OK:
		_connection = null
		status_changed.emit("Falha ao criar socket local (erro %d)" % error)
		return error
	_server_peer = _connection.connect_to_host(ip, port)
	if _server_peer == null:
		_connection.destroy()
		_connection = null
		status_changed.emit("Falha ao abrir conexao com %s:%d" % [ip, port])
		return FAILED
	status_changed.emit("Conectando a %s:%d ..." % [ip, port])
	return OK

func disconnect_from_server() -> void:
	var was_connected: bool = is_connected_to_server()
	if _connection != null:
		_connection.destroy()
	_connection = null
	_server_peer = null
	if was_connected:
		status_changed.emit("Desconectado do servidor")
		disconnected_from_server.emit()

func request_rooms() -> void:
	if not _send_ok():
		return
	RefreshRequestClass.create().send(_server_peer)

func send_grade(room_id: int, team_index: int, grade: float, comment: String) -> bool:
	if not _send_ok():
		return false
	MinigameGradePkt.create(room_id, team_index, grade, comment).send(_server_peer)
	status_changed.emit("Nota %.1f enviada para a sala %d (dupla %d)" % [grade, room_id, team_index])
	return true

func _send_ok() -> bool:
	if not is_connected_to_server():
		status_changed.emit("Sem conexao com o servidor")
		return false
	return true

func _process(_delta: float) -> void:
	if _connection == null:
		return
	_service_events()

# Mesmo laço do server: consome todos os eventos disponiveis por frame.
func _service_events() -> void:
	var event: Array = _connection.service()
	while event[0] != ENetConnection.EventType.EVENT_NONE:
		var peer: ENetPacketPeer = event[1]
		match event[0]:
			ENetConnection.EventType.EVENT_CONNECT:
				_on_connected(peer)
			ENetConnection.EventType.EVENT_DISCONNECT:
				_on_disconnected(peer)
				return
			ENetConnection.EventType.EVENT_RECEIVE:
				_on_receive(peer)
			ENetConnection.EventType.EVENT_ERROR:
				status_changed.emit("Erro de transporte na conexao com o servidor")
		event = _connection.service()

func _on_connected(peer: ENetPacketPeer) -> void:
	_server_peer = peer
	# Primeiro passo do protocolo: o server so encaminha submissoes e aceita
	# notas depois que este peer manda PROFESSOR_HELLO.
	ProfessorHelloPkt.create().send(peer)
	status_changed.emit("Conectado ao servidor. Registrado como professor.")
	connected_to_server.emit()
	request_rooms()

func _on_disconnected(_peer: ENetPacketPeer) -> void:
	var was_connected: bool = is_connected_to_server()
	_server_peer = null
	if _connection != null:
		_connection.destroy()
		_connection = null
	status_changed.emit("Conexao com o servidor encerrada")
	if was_connected:
		disconnected_from_server.emit()

func _on_receive(peer: ENetPacketPeer) -> void:
	var data: PackedByteArray = peer.get_packet()
	if data.is_empty():
		return
	match int(data.decode_u8(0)):
		PacketTypeClass.PACKET_TYPE.PEER_ID:
			var peer_id: PeerId = PeerId.create_from_data(data)
			peer_id_received.emit(peer_id.id)
		PacketTypeClass.PACKET_TYPE.REFRESH:
			rooms_received.emit(RefreshClass.create_from_data(data))
		PacketTypeClass.PACKET_TYPE.MINIGAME_SUBMISSION:
			submission_received.emit(MinigameSubmissionPkt.create_from_data(data))
		# PEER_ID/REFRESH ja tratados; JOIN_ROOM/HAS_* sao fluxo de aluno.

func _exit_tree() -> void:
	disconnect_from_server()
