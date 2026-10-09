extends Control

var array: Array[int]
const ROOM = preload("uid://5bwjrpxewel")
@onready var room_list_container: VBoxContainer = $Panel/VBoxContainer/ScrollContainer/VBoxContainer
@onready var ip_row: HBoxContainer = $Panel/VBoxContainer/IpRow
@onready var ip_line: LineEdit = $Panel/VBoxContainer/IpRow/IpLine
@onready var status_label: Label = $Panel/VBoxContainer/StatusLabel
@onready var new_room_button: Button = $Panel/VBoxContainer/HBoxContainer/NewRoom
@onready var back_button: Button = $Panel/VBoxContainer/BackToMenu
@onready var refresh_button: Button = $Panel/VBoxContainer/HBoxContainer/Refresh
@onready var scroll_container: ScrollContainer = $Panel/VBoxContainer/ScrollContainer

# Papel so pode ser decidido apos o PEER_ID chegar (my_id deixa de ser -1).
# Em _ready ele ainda pode estar -1; e -1 % 4 == -1 em GDScript (modulo C-style
# preserva sinal), entao ninguem viraria host. Decidimos no primeiro refresh_rooms,
# que so dispara depois do PEER_ID (ordem garantida pelo server em peer_connected).
var role_decided: bool = false

func _ready() -> void:
	ClientPacketHandler.created_room.connect(create_join_room)
	ClientPacketHandler.room_refresh.connect(refresh_rooms)
	ip_line.text = ClientPacketHandler.get_ipv4()
	# Estado neutro ate a primeira resposta do servidor.
	ip_row.visible = false
	new_room_button.visible = false
	# Botao Voltar removido por design: usuario nao retorna do lobby de salas.
	back_button.visible = false
	status_label.text = "Aguardando servidor..."
	RefreshRequestClass.create().send(ProtNetworkHandler.server_peer)

func _on_back_to_menu_button_down() -> void:
	get_tree().change_scene_to_file("res://prototype/scenes/main_menu.tscn")

	#send a room creation packet to server_packet_handler when the new room button is pressed
func _on_new_room_button_down() -> void:
	var ip: String = ip_line.text.strip_edges()
	if ip.is_empty():
		status_label.text = "Informe o IP da sua maquina antes de criar a sala."
		return
	if ip.count(".") != 3:
		status_label.text = "IP invalido. Use formato IPv4 (ex.: 192.168.0.10)."
		return
	status_label.text = ""
	ClientPacketHandler.my_ip = ip
	RoomRequestClass.create(ClientPacketHandler.my_id).send(ProtNetworkHandler.server_peer)
	return

	#send a refresh packet which returns a list of rooms in the refresh request packet
func _on_refresh_button_down() -> void:
	RefreshRequestClass.create().send(ProtNetworkHandler.server_peer)

func refresh_rooms(summaries: Array[RoomSummary]) -> void:
	if not role_decided:
		role_decided = true
		# Servidor envia PEER_ID antes do REFRESH, entao aqui my_id ja esta setado.
		if ClientPacketHandler.my_id % 4 == 0:
			# Host: so cria sala. Sem lista, sem refresh, sem opcao de entrar em outra.
			ip_row.visible = true
			new_room_button.visible = true
			new_room_button.text = "Criar sala"
			refresh_button.visible = false
			scroll_container.visible = false
			status_label.text = "Voce sera o host. Informe seu IP e clique em Criar sala."
			ip_line.grab_focus()
		else:
			ip_row.visible = false
			new_room_button.visible = false
			refresh_button.visible = true
			scroll_container.visible = true
			status_label.text = ""

	# Host nao entra em sala existente; lista escondida + nao popular evita o caminho.
	if ClientPacketHandler.my_id % 4 == 0:
		return

	for child in room_list_container.get_children():
		child.queue_free()
	for summary in summaries:
		add_room_to_list(summary)

func create_join_room(room_id: int) -> void:
	var summary: RoomSummary = RoomSummary.new()
	summary.id = room_id
	summary.player_count = 1
	summary.player_names = [ClientPacketHandler.temporary_player_name]
	var room_item: RoomItem = ROOM.instantiate()
	room_list_container.add_child(room_item)
	room_item.setup_room(summary)
	room_item.auto_join()

func add_room_to_list(summary: RoomSummary) -> void:
	var room_item: RoomItem = ROOM.instantiate()
	room_list_container.add_child(room_item)
	room_item.setup_room(summary)
