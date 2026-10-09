extends Node

# Lado SERVER do teste de integracao client <-> server.
# Rodar por Tools/networkTests/run.sh — ele tambem sobe o fluxo de UI do
# lobby no projeto client/ (Godot 4.6). Este projeto e Godot 4.4 mono, entao
# os dois lados rodam em processos separados.
#
# Este processo: sobe o server, espera o lobby do client criar a sala
# (ROOM_INFO) e o segundo jogador entrar (JOIN_REQUEST -> JOIN_ROOM).
# Os marcadores .server_ready/.room_created sincronizam o run.sh.
# Exit code 0 = tudo ok.

const SERVER_IP: String = "127.0.0.1"
const SERVER_PORT: int = 42069
const READY_MARKER: String = "res://Tools/networkTests/.server_ready"
const ROOM_MARKER: String = "res://Tools/networkTests/.room_created"
const WAIT_TIMEOUT_S: float = 45.0

var _checks: int = 0
var _failures: Array[String] = []

func _ready() -> void:
	_erase_marker(READY_MARKER)
	_erase_marker(ROOM_MARKER)

	var started: bool = ProtNetworkHandler.start_server(SERVER_IP, SERVER_PORT)
	_check(started, "server iniciou em %s:%d" % [SERVER_IP, SERVER_PORT])
	if not started:
		return
	_write_marker(READY_MARKER)

	# O host do client so manda o ROOM_INFO depois do START_ROOM.
	var got_room: bool = await _wait_until(func() -> bool: return _first_room_id() != -1)
	_check(got_room, "server registrou a sala criada pelo lobby (ROOM_INFO)")
	if not got_room:
		return
	var room_id: int = _first_room_id()
	_write_marker(ROOM_MARKER)

	var got_two: bool = await _wait_until(func() -> bool: return _max_players() >= 2)
	_check(got_two, "server registrou os 2 jogadores na sala %d" % room_id)

	_finish()

func _first_room_id() -> int:
	if ServerPacketHandler.rooms.is_empty():
		return -1
	return int(ServerPacketHandler.rooms.keys()[0])

func _max_players() -> int:
	var best: int = 0
	for room_id in ServerPacketHandler.rooms:
		best = maxi(best, ServerPacketHandler.rooms[room_id].current_players.size())
	return best

func _write_marker(path: String) -> void:
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		printerr("nao foi possivel escrever o marcador " + path)
		return
	file.store_line(str(Time.get_ticks_msec()))
	file.close()

func _erase_marker(path: String) -> void:
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(path))

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

func _finish() -> void:
	print("")
	print("Testes [server]: %d | falhas: %d" % [_checks, _failures.size()])
	for failure in _failures:
		printerr("FALHOU: " + failure)
	if _failures.is_empty():
		print("RESULTADO: OK (server)")
	get_tree().quit(1 if not _failures.is_empty() else 0)
