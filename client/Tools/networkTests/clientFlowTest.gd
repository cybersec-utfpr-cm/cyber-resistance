extends Node

# Lado CLIENT do teste de integracao client <-> server. Rodar por
# Tools/networkTests/run.sh na raiz do repo — ele sobe o server antes.
# Direto, ficaria:
#
#   godot --headless --path client res://Tools/networkTests/clientFlowTest.tscn -- --mode=host
#   godot --headless --path client res://Tools/networkTests/clientFlowTest.tscn -- --mode=guest
#
# host: conecta pelo lobby, cria a sala pela UI e fica vivo (mantem a sala
#       aberta) ate o run.sh encerrar o processo.
# guest: entra na sala do host pela UI e confirma a entrada (JOIN_ROOM).
# Exit code 0 = tudo ok.

const LOBBY_SCENE: String = "res://prototype/scenes/menu/client_main.tscn"
const SERVER_IP: String = "127.0.0.1"
const SERVER_PORT: int = 42069
const WAIT_TIMEOUT_S: float = 10.0

var _checks: int = 0
var _failures: Array[String] = []
var _mode: String = "host"

func _ready() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--mode="):
			_mode = arg.trim_prefix("--mode=")

	if _mode == "host":
		await _host_flow()
	else:
		await _guest_flow()

	print("")
	print("Testes [%s]: %d | falhas: %d" % [_mode, _checks, _failures.size()])
	for failure in _failures:
		printerr("FALHOU: " + failure)
	if not _failures.is_empty():
		get_tree().quit(1)
		return
	print("RESULTADO: OK (%s)" % _mode)
	if _mode != "host":
		get_tree().quit(0)
		return
	# Host fica vivo ate o run.sh matar o processo: a sala precisa continuar
	# aberta enquanto o guest entra e o server confirma os 2 jogadores.
	while true:
		await get_tree().process_frame

func _host_flow() -> void:
	var lobby: Control = _spawn_lobby()
	await get_tree().process_frame

	_connect_lobby(lobby)
	await _wait_until(func() -> bool: return ClientPacketHandler.my_id != -1)
	_check(ClientPacketHandler.my_id != -1, "lobby recebeu o PEER_ID")
	if ClientPacketHandler.my_id == -1:
		return

	await _wait_until(func() -> bool: return lobby.get("role_decided"))
	_check(bool(lobby.get("is_host_role")), "cliente %d recebeu o papel de host" % ClientPacketHandler.my_id)
	_check(ClientPacketHandler.my_id % 4 == 0, "id do host (%d) % 4 == 0" % ClientPacketHandler.my_id)
	if not bool(lobby.get("is_host_role")):
		return

	_check_layout(lobby, "lobby do client")

	lobby.get_node("Margin/Root/PlayerRow/NameLine").text = "Host Alpha"
	lobby.get_node("Margin/Root/PlayerRow/HostIpLine").text = SERVER_IP
	lobby.get_node("Margin/Root/Split/RoomsBox/RoomButtons/CreateRoomButton").pressed.emit()

	var status: Label = lobby.get_node("Margin/Root/ConnectionRow/StatusLabel")
	await _wait_until(func() -> bool: return str(status.text).contains("criada"))
	_check(str(status.text).contains("criada"), "UI do host mostrou a sala criada")

func _guest_flow() -> void:
	var lobby: Control = _spawn_lobby()
	await get_tree().process_frame

	_connect_lobby(lobby)
	await _wait_until(func() -> bool: return ClientPacketHandler.my_id != -1)
	_check(ClientPacketHandler.my_id != -1, "lobby recebeu o PEER_ID")
	if ClientPacketHandler.my_id == -1:
		return
	_check(ClientPacketHandler.my_id % 4 != 0, "id do guest (%d) nao e de host" % ClientPacketHandler.my_id)

	await _wait_until(func() -> bool: return lobby.get("role_decided"))
	_check(not bool(lobby.get("is_host_role")), "cliente %d recebeu o papel de jogador" % ClientPacketHandler.my_id)
	if bool(lobby.get("is_host_role")):
		return

	var room_list: ItemList = lobby.get_node("Margin/Root/Split/RoomsBox/RoomList")
	await _wait_until(func() -> bool: return room_list.item_count > 0)
	_check(room_list.item_count > 0, "lista de salas mostrou a sala do host")
	if room_list.item_count == 0:
		return
	_check(str(room_list.get_item_text(0)).contains("Host Alpha"), "lista mostra o nome do host")

	room_list.select(0)
	room_list.item_selected.emit(0)
	lobby.get_node("Margin/Root/Split/RoomsBox/RoomButtons/JoinButton").pressed.emit()

	var status: Label = lobby.get_node("Margin/Root/ConnectionRow/StatusLabel")
	await _wait_until(func() -> bool: return str(status.text).contains("Entrando na sala"))
	_check(str(status.text).contains("Entrando na sala"), "UI do guest confirmou a entrada (JOIN_ROOM)")

func _spawn_lobby() -> Control:
	var lobby: Control = (load(LOBBY_SCENE) as PackedScene).instantiate()
	# O lobby abre o world ao entrar; o teste so verifica a tela de salas.
	lobby.set("auto_open_game", false)
	add_child(lobby)
	return lobby

func _connect_lobby(lobby: Control) -> void:
	lobby.get_node("Margin/Root/ConnectionRow/IpLine").text = SERVER_IP
	lobby.get_node("Margin/Root/ConnectionRow/PortLine").text = str(SERVER_PORT)
	lobby.get_node("Margin/Root/ConnectionRow/ConnectButton").pressed.emit()

func _check_layout(scene: Control, label: String) -> void:
	var viewport_height: float = _viewport_height()
	var min_size: Vector2 = (scene.get_node("Margin/Root") as Control).get_combined_minimum_size()
	if ThemeDB.fallback_font.get_height(18) > 40.0:
		print("  aviso - metricas de fonte infladas; layout do %s nao verificado (min=%.0fpx)" % [label, min_size.y])
	elif viewport_height > 0.0:
		_check(
			min_size.y <= viewport_height,
			"%s cabe na janela (%.0f <= %.0f)" % [label, min_size.y, viewport_height]
		)

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
