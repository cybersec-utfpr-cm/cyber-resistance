extends Node

# Teste de integracao professor <-> server (rodar headless):
#   godot --headless --path . res://Tools/networkTests/professorServerTest.tscn
#
# Cobre: registro do professor (PROFESSOR_HELLO), lista de salas (REFRESH),
# submissao de minigame encaminhada ao professor e nota devolvida
# (MINIGAME_GRADE) ao host da sala. Exit code 0 = tudo ok.

const MENU_SCENE: String = "res://Scenes/Interfaces/main_menu.tscn"
const PROFESSOR_SCENE: String = "res://Scenes/Professor/professor_main.tscn"
const SERVER_IP: String = "127.0.0.1"
const SERVER_PORT: int = 42069
const WAIT_TIMEOUT_S: float = 6.0

var _checks: int = 0
var _failures: Array[String] = []

# Cliente simulado do aluno/host de sala: usa a mesma API de baixo nivel
# (ENetConnection) que o server e que o professor usam.
class StudentSim extends Node:
	var conn: ENetConnection = null
	var server_peer: ENetPacketPeer = null
	var connected: bool = false
	var packets: Array = []

	func start(ip: String, port: int) -> Error:
		conn = ENetConnection.new()
		var error: Error = conn.create_host()
		if error != OK:
			conn = null
			return error
		server_peer = conn.connect_to_host(ip, port)
		return OK if server_peer != null else FAILED

	func send(packet: PacketTypeClass) -> void:
		packet.send(server_peer)

	func _process(_delta: float) -> void:
		if conn == null:
			return
		var event: Array = conn.service()
		while event[0] != ENetConnection.EventType.EVENT_NONE:
			var peer: ENetPacketPeer = event[1]
			match event[0]:
				ENetConnection.EventType.EVENT_CONNECT:
					connected = true
				ENetConnection.EventType.EVENT_RECEIVE:
					packets.append(peer.get_packet())
			event = conn.service()

	func find_type(packet_type: int) -> PackedByteArray:
		for packet in packets:
			if packet.size() > 0 and int(packet.decode_u8(0)) == packet_type:
				return packet
		return PackedByteArray()

	func has_type(packet_type: int) -> bool:
		return not find_type(packet_type).is_empty()

	func stop() -> void:
		if conn != null:
			conn.destroy()
		conn = null

func _ready() -> void:
	await _check_menu()
	await _check_professor_flow()

	print("")
	print("Testes: %d | falhas: %d" % [_checks, _failures.size()])
	for failure in _failures:
		printerr("FALHOU: " + failure)
	if _failures.is_empty():
		print("RESULTADO: OK")
	get_tree().quit(1 if not _failures.is_empty() else 0)

func _check_menu() -> void:
	var menu: Node = (load(MENU_SCENE) as PackedScene).instantiate()
	add_child(menu)
	await get_tree().process_frame
	await get_tree().process_frame

	var viewport_height: float = _viewport_height()
	var panel: Control = menu.get_node("Center/MenuPanel")
	_check(panel.size.y > 0.0, "painel do menu renderizou (%.0fpx)" % panel.size.y)
	# No headless com window/stretch/mode=canvas_items as metricas de fonte saem
	# infladas (~3x) e a medicao nao representa o editor; nesse caso so avisamos.
	if ThemeDB.fallback_font.get_height(18) > 40.0:
		print("  aviso - metricas de fonte infladas neste ambiente; painel=%.0fpx (viewport configurada=%.0fpx)" % [
			panel.size.y, viewport_height
		])
		_dump_menu_sizes(menu)
	elif viewport_height > 0.0:
		_check(
			panel.size.y <= viewport_height,
			"painel do menu cabe na janela (%.0f <= %.0f)" % [panel.size.y, viewport_height]
		)
		if panel.size.y > viewport_height:
			_dump_menu_sizes(menu)

	var professor_button: Button = menu.get_node(
		"Center/MenuPanel/MenuMargin/MenuContent/Buttons/ProfessorButton"
	)
	_check(professor_button != null, "ProfessorButton existe na cena")
	_check(
		professor_button.pressed.get_connections().size() > 0,
		"ProfessorButton esta ligado ao MainMenu"
	)
	_check(
		str(menu.get("ProfessorScenePath")) == PROFESSOR_SCENE,
		"ProfessorScenePath aponta para a cena do professor"
	)
	menu.queue_free()
	await get_tree().process_frame

func _dump_menu_sizes(menu: Node) -> void:
	var content: Control = menu.get_node("Center/MenuPanel/MenuMargin/MenuContent")
	for child in content.get_children():
		if child is Control and (child as Control).visible:
			print("      conteudo %-18s %6.0f" % [child.name, (child as Control).size.y])
		if child is Label:
			var label: Label = child as Label
			var font: Font = label.get_theme_font("font")
			var font_size: int = label.get_theme_font_size("font_size")
			var needed: float = font.get_string_size(label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
			print("        label %-16s texto=%.0f disponivel=%.0f font=%d altura_font=%.0f asc=%.0f desc=%.0f classe=%s" % [label.name, needed, label.size.x, font_size, font.get_height(font_size), font.get_ascent(font_size), font.get_descent(font_size), font.get_class()])
	var buttons: Control = menu.get_node("Center/MenuPanel/MenuMargin/MenuContent/Buttons")
	for child in buttons.get_children():
		if child is Control:
			print("      botao   %-18s %6.0f" % [child.name, (child as Control).size.y])

func _check_professor_flow() -> void:
	_check(
		ProtNetworkHandler.start_server(SERVER_IP, SERVER_PORT),
		"server iniciou em %s:%d" % [SERVER_IP, SERVER_PORT]
	)

	var professor: Control = (load(PROFESSOR_SCENE) as PackedScene).instantiate()
	add_child(professor)
	await get_tree().process_frame
	await get_tree().process_frame

	var viewport_height: float = _viewport_height()
	var professor_min: Vector2 = (professor.get_node("Margin/Root") as Control).get_combined_minimum_size()
	if ThemeDB.fallback_font.get_height(18) > 40.0:
		print("  aviso - metricas de fonte infladas; layout do professor nao verificado (min=%.0fpx)" % professor_min.y)
	elif viewport_height > 0.0:
		_check(
			professor_min.y <= viewport_height,
			"console do professor cabe na janela (%.0f <= %.0f)" % [professor_min.y, viewport_height]
		)

	professor.get_node("Margin/Root/ConnectionRow/IpLine").text = SERVER_IP
	professor.get_node("Margin/Root/ConnectionRow/PortLine").text = str(SERVER_PORT)
	professor.get_node("Margin/Root/ConnectionRow/ConnectButton").pressed.emit()

	_check(
		await _wait_until(func() -> bool: return ServerPacketHandler.professor_peer != null),
		"server registrou o professor (PROFESSOR_HELLO)"
	)

	var student: StudentSim = StudentSim.new()
	add_child(student)
	_check(student.start(SERVER_IP, SERVER_PORT) == OK, "aluno abriu conexao")
	_check(await _wait_until(func() -> bool: return student.connected), "aluno conectou")

	# Fluxo real de sala: pede sala, recebe o id e registra as informacoes.
	student.send(RoomRequestClass.create(1))
	_check(
		await _wait_until(func() -> bool: return student.has_type(PacketTypeClass.PACKET_TYPE.START_ROOM)),
		"aluno recebeu START_ROOM"
	)
	var start_data: PackedByteArray = student.find_type(PacketTypeClass.PACKET_TYPE.START_ROOM)
	if start_data.is_empty():
		return
	var start_packet: StartRoomClass = StartRoomClass.create_from_data(start_data)
	student.send(RoomInfoClass.create(1, start_packet.room, 42100, SERVER_IP, "Dupla Alpha"))
	_check(
		await _wait_until(func() -> bool: return ServerPacketHandler.rooms.has(start_packet.room)),
		"server registrou a sala %d" % start_packet.room
	)

	# Atualiza a lista de salas pela UI (REFRESH_REQUEST).
	professor.get_node("Margin/Root/Split/RoomsBox/RefreshButton").pressed.emit()
	var room_list: ItemList = professor.get_node("Margin/Root/Split/RoomsBox/RoomList")
	_check(
		await _wait_until(func() -> bool: return room_list.item_count > 1),
		"UI do professor listou a sala %d" % start_packet.room
	)

	var teams: Array = [
		{
			"members": [1, 2],
			"total_msec": 45200,
			"answers": ["SQL Injection", "VPN"],
			"submission_msecs": [12000, 33200]
		},
		{
			"members": [3, 4],
			"total_msec": 51000,
			"answers": ["Engenharia social", "Firewall"],
			"submission_msecs": [20000, 31000]
		}
	]
	student.send(MinigameSubmissionPkt.create(start_packet.room, teams))

	var submission_list: ItemList = professor.get_node(
		"Margin/Root/Split/SubmissionsBox/SubmissionList"
	)
	_check(
		await _wait_until(func() -> bool: return submission_list.item_count >= 2),
		"UI do professor recebeu as 2 submissoes"
	)
	var detail: RichTextLabel = professor.get_node("Margin/Root/Split/SubmissionsBox/Detail")
	_check(detail.text.contains("SQL Injection"), "detalhe mostra a resposta do aluno")

	# Correcao pela UI: nota + comentario voltam ao host da sala.
	professor.get_node("Margin/Root/Split/SubmissionsBox/GradeBox/GradeSpin").value = 8.5
	professor.get_node("Margin/Root/Split/SubmissionsBox/GradeBox/CommentLine").text = "bom trabalho"
	professor.get_node("Margin/Root/Split/SubmissionsBox/GradeBox/SendGradeButton").pressed.emit()

	_check(
		await _wait_until(func() -> bool: return student.has_type(PacketTypeClass.PACKET_TYPE.MINIGAME_GRADE)),
		"host da sala recebeu MINIGAME_GRADE"
	)
	var grade_data: PackedByteArray = student.find_type(PacketTypeClass.PACKET_TYPE.MINIGAME_GRADE)
	if grade_data.is_empty():
		return
	var grade_packet: MinigameGradePkt = MinigameGradePkt.create_from_data(grade_data)
	_check(grade_packet.room_id == start_packet.room, "nota chegou na sala certa")
	_check(grade_packet.team_index == 0, "nota chegou na dupla certa")
	_check(absf(grade_packet.grade - 8.5) < 0.001, "nota valor correto (%.1f)" % grade_packet.grade)
	_check(grade_packet.comment == "bom trabalho", "comentario preservado")

	student.stop()

func _viewport_height() -> float:
	var value: Variant = ProjectSettings.get_setting("display/window/size/viewport_height", 0)
	return float(value) if value != null else 0.0

func _wait_until(predicate: Callable, timeout_s: float = WAIT_TIMEOUT_S) -> bool:
	var elapsed: float = 0.0
	while elapsed < timeout_s:
		if predicate.call():
			return true
		await get_tree().create_timer(0.05).timeout
		elapsed += 0.05
	return predicate.call()

func _check(condition: bool, description: String) -> void:
	_checks += 1
	if condition:
		print("  ok - " + description)
	else:
		_failures.append(description)
		printerr("  FALHOU - " + description)
