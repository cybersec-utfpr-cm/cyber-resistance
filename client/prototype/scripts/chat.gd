extends Control

const TEAM_COLOR_A := "#3a8dff"  # azul — dupla A
const TEAM_COLOR_B := "#ff5a5a"  # vermelho — dupla B

@onready var display: RichTextLabel = $Panel/VBoxContainer/Display
@onready var input_line: LineEdit = $Panel/VBoxContainer/HBoxContainer/InputLine
@onready var send: Button = $Panel/VBoxContainer/HBoxContainer/Send

var is_host: bool

func _ready() -> void:
	is_host = GamePacketHandler.is_host
	input_line.text_submitted.connect(_on_text_submitted)
	if is_host:
		# Host receives text packets from players via player_text_signal.
		PlayerHostPacketHandler.player_text_signal.connect(host_text)
	else:
		# Players receive text packets from host via host_text_signal.
		PlayerHostPacketHandler.host_text_signal.connect(player_text)

func _on_text_submitted(_new_text: String) -> void:
	_on_send_pressed()
	input_line.release_focus()

func _on_send_pressed() -> void:
	var raw_text = input_line.text
	if raw_text.is_empty():
		return
	var team: int = _current_team_tag()
	var packet: ChatTextClass = ChatTextClass.create(ClientPacketHandler.temporary_player_name, raw_text, team)
	if is_host:
		# Host: broadcast NÃO volta para o próprio host, então mostramos local.
		append_message(packet.sender_name, packet.text, team)
		packet.broadcast(GamePacketHandler.host_connection)
	else:
		# Player: o host rebroadcasta e o nosso próprio peer recebe de volta,
		# então NÃO chamamos append_message aqui (evitar duplicata).
		packet.send(GamePacketHandler.host_peer)
	input_line.clear()
	input_line.release_focus()

func player_text(data: PackedByteArray) -> void:
	var text_packet: ChatTextClass = ChatTextClass.create_from_data(data)
	if not _should_display(text_packet.sender_team):
		return
	append_message(text_packet.sender_name, text_packet.text, text_packet.sender_team)

func host_text(data: PackedByteArray) -> void:
	var text_packet: ChatTextClass = ChatTextClass.create_from_data(data)
	# Host sempre rebroadcasta para que cada cliente decida pelo próprio filtro.
	text_packet.broadcast(GamePacketHandler.host_connection)
	if not _should_display(text_packet.sender_team):
		return
	append_message(text_packet.sender_name, text_packet.text, text_packet.sender_team)

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_accept"):
		if not input_line.has_focus():
			input_line.grab_focus()
			get_viewport().set_input_as_handled()

func append_message(sender_name: String, msg: String, team: int = ChatTextClass.TEAM_NONE) -> void:
	var display_name: String = "me" if sender_name == ClientPacketHandler.temporary_player_name else sender_name
	var color: String = _team_color(team)
	var line: String
	if color.is_empty():
		line = "%s: %s\n" % [display_name, msg]
	else:
		# Pintamos só o nome do remetente; o texto fica na cor padrão para legibilidade.
		line = "[color=%s]%s[/color]: %s\n" % [color, display_name, msg]
	display.append_text(line)

func _current_team_tag() -> int:
	# Fora do minigame, ClientPacketHandler.minigame_team é -1; mandamos TEAM_NONE.
	var t: int = ClientPacketHandler.minigame_team
	return ChatTextClass.TEAM_NONE if t < 0 else t

func _should_display(sender_team: int) -> bool:
	# Fora do minigame: mostra tudo. Dentro: só do meu time.
	# (Mensagens "antigas" com TEAM_NONE só caem aqui se vierem de fora do minigame,
	# o que não acontece em sessão normal; mantemos a regra simples mesmo assim.)
	if ClientPacketHandler.minigame_team < 0:
		return true
	return sender_team == ClientPacketHandler.minigame_team

func _team_color(team: int) -> String:
	match team:
		0: return TEAM_COLOR_A
		1: return TEAM_COLOR_B
		_: return ""
