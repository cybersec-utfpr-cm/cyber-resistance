extends Control

# Lobby do client: conexao com o server central (autoload ProtNetworkHandler),
# lista/criacao/entrada de sala (autoload ClientPacketHandler) e abertura do
# jogo (world.tscn) ao entrar. E a etapa entre o mainMenu e o world:
# mainMenu -> aqui -> world.

const MENU_SCENE: String = "res://prototype/scenes/menu/mainMenu.tscn"
const GAME_SCENE: String = "res://prototype/scenes/scenarios/world.tscn"

# O teste headless desliga a abertura do jogo para verificar o lobby.
@export var auto_open_game: bool = true

@onready var back_button: Button = $Margin/Root/Header/BackButton
@onready var ip_line: LineEdit = $Margin/Root/ConnectionRow/IpLine
@onready var port_line: LineEdit = $Margin/Root/ConnectionRow/PortLine
@onready var connect_button: Button = $Margin/Root/ConnectionRow/ConnectButton
@onready var status_label: Label = $Margin/Root/ConnectionRow/StatusLabel
@onready var name_line: LineEdit = $Margin/Root/PlayerRow/NameLine
@onready var host_ip_label: Label = $Margin/Root/PlayerRow/HostIpLabel
@onready var host_ip_line: LineEdit = $Margin/Root/PlayerRow/HostIpLine
@onready var rooms_title: Label = $Margin/Root/Split/RoomsBox/RoomsTitle
@onready var room_list: ItemList = $Margin/Root/Split/RoomsBox/RoomList
@onready var refresh_button: Button = $Margin/Root/Split/RoomsBox/RoomButtons/RefreshButton
@onready var create_button: Button = $Margin/Root/Split/RoomsBox/RoomButtons/CreateRoomButton
@onready var join_button: Button = $Margin/Root/Split/RoomsBox/RoomButtons/JoinButton

# Papel (host x jogador) so decide no primeiro REFRESH: o server manda o
# PEER_ID antes, entao my_id ja esta setado — mesma regra do antigo
# multiplayer.gd (my_id % 4 == 0 vira host; -1 % 4 == -1 no GDScript).
var role_decided: bool = false
var is_host_role: bool = false
var _entering_game: bool = false

func _ready() -> void:
	port_line.text = "42069"
	name_line.text = "player"

	back_button.pressed.connect(_on_back_pressed)
	connect_button.pressed.connect(_on_connect_pressed)
	refresh_button.pressed.connect(_on_refresh_pressed)
	create_button.pressed.connect(_on_create_pressed)
	join_button.pressed.connect(_on_join_pressed)
	room_list.item_selected.connect(_on_room_selected)

	ClientPacketHandler.room_refresh.connect(_on_room_refresh)
	ClientPacketHandler.created_room.connect(_on_created_room)
	ClientPacketHandler.join_room.connect(_on_join_room)
	ClientPacketHandler.quit_room.connect(_on_quit_room)
	ProtNetworkHandler.on_peer_connected.connect(_on_peer_connected)
	ProtNetworkHandler.on_connection_error.connect(_on_connection_error)

	_apply_neutral_layout()
	if _server_connected():
		# Caminho normal: mainMenu ja conectou e trocou para esta cena.
		ip_line.text = ProtNetworkHandler.server_ip
		host_ip_line.text = _local_ipv4()
		_apply_connected_state()
		_set_status("Conectado a %s:%s" % [ip_line.text, port_line.text])
		# O REFRESH inicial chegou enquanto o mainMenu ainda era a cena; pede outro.
		_request_rooms()
	else:
		ip_line.text = "127.0.0.1"
		host_ip_line.text = _local_ipv4()
		_apply_disconnected_state()
		_set_status("Desconectado. Informe o IP do servidor e conecte.")
	_update_room_controls()

# ---------------------------------------------------------------- conexao

func _on_connect_pressed() -> void:
	if ProtNetworkHandler.server_connection != null:
		# Conectando ou conectado: o botao vira Desconectar.
		ProtNetworkHandler.stop_client()
		_reset_role()
		_clear_rooms()
		_apply_disconnected_state()
		_set_status("Desconectado.")
		return
	var ip: String = ip_line.text.strip_edges()
	if ip.is_empty():
		_set_status("Informe o IP do servidor")
		return
	var port_text: String = port_line.text.strip_edges()
	if not port_text.is_valid_int() or int(port_text) < 1 or int(port_text) > 65535:
		_set_status("Porta invalida (1-65535)")
		return
	# A conexao e assincrona; o botao volta a ficar ativo nos callbacks.
	connect_button.disabled = true
	ip_line.editable = false
	port_line.editable = false
	_set_status("Conectando a %s:%d ..." % [ip, int(port_text)])
	ProtNetworkHandler.start_client(ip, int(port_text))
	if ProtNetworkHandler.server_peer == null:
		# start_client nao conseguiu nem criar o socket local.
		_apply_disconnected_state()
		_set_status("Falha ao abrir a conexao local")

func _on_peer_connected() -> void:
	_apply_connected_state()
	_set_status("Conectado a %s" % ProtNetworkHandler.server_ip)
	_request_rooms()
	_update_room_controls()

func _on_connection_error() -> void:
	# O network_handler ja troca para o mainMenu neste caminho; aqui so
	# deixamos a UI coerente caso a cena ainda exista.
	_apply_disconnected_state()
	_set_status("Falha de conexao com o servidor")

func _on_back_pressed() -> void:
	if ProtNetworkHandler.server_connection != null:
		ProtNetworkHandler.stop_client()
	get_tree().change_scene_to_file(MENU_SCENE)

# ---------------------------------------------------------------- salas

func _on_refresh_pressed() -> void:
	_request_rooms()

func _request_rooms() -> void:
	if not _server_connected():
		_set_status("Sem conexao com o servidor")
		return
	RefreshRequestClass.create().send(ProtNetworkHandler.server_peer)

func _on_room_refresh(summaries: Array[RoomSummary]) -> void:
	if not role_decided and ClientPacketHandler.my_id != -1:
		role_decided = true
		is_host_role = ClientPacketHandler.my_id % 4 == 0
		_apply_role()
	_rebuild_room_list(summaries)
	_update_room_controls()

func _on_create_pressed() -> void:
	var player_name: String = name_line.text.strip_edges()
	if player_name.is_empty():
		_set_status("Informe o nome do jogador")
		return
	var host_ip: String = host_ip_line.text.strip_edges()
	if host_ip.is_empty():
		_set_status("Informe o IP da sua maquina antes de criar a sala.")
		return
	if not _server_connected():
		_set_status("Sem conexao com o servidor")
		return
	if ClientPacketHandler.my_id == -1:
		_set_status("Aguardando o id do servidor ...")
		return
	# O ClientPacketHandler usa esses valores no START_ROOM -> ROOM_INFO.
	ClientPacketHandler.temporary_player_name = player_name
	ClientPacketHandler.my_ip = host_ip
	RoomRequestClass.create(ClientPacketHandler.my_id).send(ProtNetworkHandler.server_peer)
	_set_status("Solicitando nova sala ...")

func _on_join_pressed() -> void:
	var selected: PackedInt32Array = room_list.get_selected_items()
	if selected.is_empty():
		_set_status("Selecione uma sala para entrar")
		return
	var player_name: String = name_line.text.strip_edges()
	if player_name.is_empty():
		_set_status("Informe o nome do jogador")
		return
	if not _server_connected():
		_set_status("Sem conexao com o servidor")
		return
	if ClientPacketHandler.my_id == -1:
		_set_status("Aguardando o id do servidor ...")
		return
	var room_id: int = int(room_list.get_item_metadata(selected[0]))
	ClientPacketHandler.temporary_player_name = player_name
	JoinRequestClass.create(room_id, ClientPacketHandler.my_id, player_name).send(ProtNetworkHandler.server_peer)
	_set_status("Solicitando entrada na sala %d ..." % room_id)

func _on_room_selected(_index: int) -> void:
	_update_join_button()

func _on_created_room(room_id: int) -> void:
	_set_status("Sala %d criada" % room_id)
	_enter_game()

func _on_join_room(room_id: int) -> void:
	_set_status("Entrando na sala %d ..." % room_id)
	_enter_game()

func _on_quit_room() -> void:
	_set_status("A sala foi encerrada")

# ---------------------------------------------------------------- jogo

func _enter_game() -> void:
	if _entering_game:
		return
	_entering_game = true
	_update_room_controls()
	if auto_open_game:
		get_tree().call_deferred("change_scene_to_file", GAME_SCENE)

# ---------------------------------------------------------------- UI

func _apply_neutral_layout() -> void:
	# Sem papel decidido: lista pronta para jogador, sem Criar sala.
	create_button.visible = false
	host_ip_label.visible = false
	host_ip_line.visible = false
	rooms_title.visible = true
	room_list.visible = true
	refresh_button.visible = true
	join_button.visible = true

func _apply_role() -> void:
	if is_host_role:
		# Host so cria sala (sala de aula: 1 host a cada 4 clientes).
		create_button.visible = true
		host_ip_label.visible = true
		host_ip_line.visible = true
		rooms_title.visible = false
		room_list.visible = false
		refresh_button.visible = false
		join_button.visible = false
		_set_status("Voce sera o host da sala. Informe seu IP e clique em Criar sala.")
	else:
		create_button.visible = false
		host_ip_label.visible = false
		host_ip_line.visible = false
		rooms_title.visible = true
		room_list.visible = true
		refresh_button.visible = true
		join_button.visible = true
		_set_status("")

func _reset_role() -> void:
	role_decided = false
	is_host_role = false
	_apply_neutral_layout()

func _apply_connected_state() -> void:
	connect_button.text = "Desconectar"
	connect_button.disabled = false
	ip_line.editable = false
	port_line.editable = false

func _apply_disconnected_state() -> void:
	connect_button.text = "Conectar"
	connect_button.disabled = false
	ip_line.editable = true
	port_line.editable = true

func _rebuild_room_list(summaries: Array[RoomSummary]) -> void:
	var selected_room: int = _selected_room()
	room_list.clear()
	var ordered: Array[RoomSummary] = []
	for summary in summaries:
		ordered.append(summary)
	ordered.sort_custom(func(a: RoomSummary, b: RoomSummary) -> bool: return a.id < b.id)
	for summary in ordered:
		var label: String = "Sala %d - %d jogador(es)" % [summary.id, summary.player_count]
		if not summary.player_names.is_empty():
			label += ": " + ", ".join(PackedStringArray(summary.player_names))
		room_list.add_item(label)
		room_list.set_item_metadata(room_list.item_count - 1, summary.id)
		if summary.id == selected_room:
			room_list.select(room_list.item_count - 1)

func _update_room_controls() -> void:
	refresh_button.disabled = not _server_connected()
	create_button.disabled = (
		not _server_connected()
		or ClientPacketHandler.my_id == -1
		or _entering_game
	)
	_update_join_button()

func _update_join_button() -> void:
	join_button.disabled = (
		not _server_connected()
		or _selected_room() == -1
		or _entering_game
	)

func _selected_room() -> int:
	var selected: PackedInt32Array = room_list.get_selected_items()
	if selected.is_empty():
		return -1
	return int(room_list.get_item_metadata(selected[0]))

func _clear_rooms() -> void:
	room_list.clear()
	_update_room_controls()

func _set_status(text: String) -> void:
	status_label.text = text

# ---------------------------------------------------------------- util

func _server_connected() -> bool:
	return ProtNetworkHandler.server_peer != null \
		and ProtNetworkHandler.server_peer.get_state() == ENetPacketPeer.STATE_CONNECTED

func _local_ipv4() -> String:
	if _server_connected():
		var from_subnet: String = ClientPacketHandler.get_ipv4()
		if not from_subnet.is_empty():
			return from_subnet
	for ip in IP.get_local_addresses():
		if ip.count(".") == 3 and (ip.begins_with("192.168.") or ip.begins_with("10.") or ip.begins_with("172.")):
			return ip
	return ""
