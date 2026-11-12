extends Control

# Console do professor: conecta ao server central, lista as salas, exibe as
# submissoes de minigame recebidas e devolve nota + comentario (MINIGAME_GRADE).

const MENU_SCENE: String = "res://Scenes/Interfaces/main_menu.tscn"
const QUIZ_PATH: String = "res://professor/data/minigame_quiz.json"

@onready var back_button: Button = $Margin/Root/Header/BackButton
@onready var ip_line: LineEdit = $Margin/Root/ConnectionRow/IpLine
@onready var port_line: LineEdit = $Margin/Root/ConnectionRow/PortLine
@onready var connect_button: Button = $Margin/Root/ConnectionRow/ConnectButton
@onready var status_label: Label = $Margin/Root/ConnectionRow/StatusLabel
@onready var room_list: ItemList = $Margin/Root/Split/RoomsBox/RoomList
@onready var refresh_button: Button = $Margin/Root/Split/RoomsBox/RefreshButton
@onready var activity_option: OptionButton = get_node_or_null("Margin/Root/Split/RoomsBox/ActivityOption")
@onready var apply_activity_button: Button = get_node_or_null("Margin/Root/Split/RoomsBox/ApplyActivityButton")
@onready var submission_list: ItemList = $Margin/Root/Split/SubmissionsBox/SubmissionList
@onready var detail_label: Label = $Margin/Root/Split/SubmissionsBox/DetailLabel
@onready var detail: RichTextLabel = $Margin/Root/Split/SubmissionsBox/Detail
@onready var grade_spin: SpinBox = $Margin/Root/Split/SubmissionsBox/GradeBox/GradeSpin
@onready var comment_line: LineEdit = $Margin/Root/Split/SubmissionsBox/GradeBox/CommentLine
@onready var send_grade_button: Button = $Margin/Root/Split/SubmissionsBox/GradeBox/SendGradeButton
@onready var network: Node = $Network

# room_id -> RoomSummary (ultima atualizacao recebida)
var _rooms: Dictionary = {}
# Entradas achatadas de submissao: { room_id, team_index, activity_id, team }
var _entries: Array = []
# -1 = sem filtro de sala
var _selected_room: int = -1
var _questions: Array = []
var _activities: Array = []
var _room_activities: Dictionary = {}
var _default_activity_id: String = "defesa_redes"

func _ready() -> void:
	ip_line.text = "127.0.0.1"
	port_line.text = "42069"
	_questions = _load_questions()
	_populate_activity_option()

	network.connected_to_server.connect(_on_connected)
	network.disconnected_from_server.connect(_on_disconnected)
	network.status_changed.connect(_on_status)
	network.rooms_received.connect(_on_rooms)
	network.submission_received.connect(_on_submission)

	back_button.pressed.connect(_on_back_pressed)
	connect_button.pressed.connect(_on_connect_pressed)
	refresh_button.pressed.connect(_on_refresh_pressed)
	if apply_activity_button != null:
		apply_activity_button.pressed.connect(_on_apply_activity_pressed)
	room_list.item_selected.connect(_on_room_selected)
	submission_list.item_selected.connect(_on_submission_selected)
	send_grade_button.pressed.connect(_on_send_grade_pressed)

	_set_status("Desconectado. Informe o IP do servidor e conecte.")
	_rebuild_room_list()
	_rebuild_submission_list()
	_update_grade_controls()

func _on_back_pressed() -> void:
	network.disconnect_from_server()
	get_tree().change_scene_to_file(MENU_SCENE)

func _on_connect_pressed() -> void:
	if network.is_connected_to_server():
		network.disconnect_from_server()
		return
	var ip: String = ip_line.text.strip_edges()
	if ip.is_empty():
		_set_status("Informe o IP do servidor")
		return
	var port_text: String = port_line.text.strip_edges()
	if not port_text.is_valid_int() or int(port_text) < 1 or int(port_text) > 65535:
		_set_status("Porta invalida (1-65535)")
		return
	# O botao volta a ficar ativo no callback de status (conexao e assincrona).
	connect_button.disabled = true
	ip_line.editable = false
	port_line.editable = false
	if network.connect_to_server(ip, int(port_text)) != OK:
		_on_disconnected()

func _on_refresh_pressed() -> void:
	network.request_rooms()

func _on_connected() -> void:
	connect_button.text = "Desconectar"
	connect_button.disabled = false
	_update_grade_controls()

func _on_disconnected() -> void:
	connect_button.text = "Conectar"
	connect_button.disabled = false
	ip_line.editable = true
	port_line.editable = true
	_update_grade_controls()

func _on_status(text: String) -> void:
	_set_status(text)

func _set_status(text: String) -> void:
	status_label.text = text

func _on_rooms(refresh: RefreshClass) -> void:
	var previous_selection: int = _selected_room
	_rooms.clear()
	for summary in refresh.summaries:
		_rooms[summary.id] = summary
	_rebuild_room_list()
	# Mantem a sala filtrada mesmo com o push periodico do server.
	if previous_selection != -1:
		_select_room(previous_selection if _rooms.has(previous_selection) else -1)

func _on_submission(submission: MinigameSubmissionPkt) -> void:
	var count_before: int = _entries.size()
	var act_id: String = str(_room_activities.get(submission.room_id, _default_activity_id))
	for team_index in submission.teams.size():
		_entries.append({
			"room_id": submission.room_id,
			"team_index": team_index,
			"activity_id": act_id,
			"team": submission.teams[team_index]
		})
	_rebuild_submission_list()
	_update_grade_controls()
	if count_before == 0 and _entries.size() > 0:
		submission_list.select(0)
		_show_entry_detail(_visible_entries()[0])
	_set_status("Submissao recebida da sala %d" % submission.room_id)

func _on_apply_activity_pressed() -> void:
	if activity_option == null or activity_option.selected < 0 or activity_option.selected >= _activities.size():
		return
	var chosen: Dictionary = _activities[activity_option.selected]
	var act_id: String = str(chosen.get("id", ""))
	var act_title: String = str(chosen.get("title", act_id))
	if network.send_activity_select(_selected_room, act_id, act_title):
		if _selected_room < 0:
			_default_activity_id = act_id
			for r_id in _rooms.keys():
				_room_activities[r_id] = act_id
		else:
			_room_activities[_selected_room] = act_id

func _on_room_selected(index: int) -> void:
	_select_room(-1 if index == 0 else int(room_list.get_item_metadata(index)))

func _select_room(room_id: int) -> void:
	_selected_room = room_id
	var visible_index: int = 0
	for i in room_list.item_count:
		var item_room: int = -1 if i == 0 else int(room_list.get_item_metadata(i))
		if item_room == room_id:
			visible_index = i
			break
	if room_list.item_count > 0:
		room_list.select(visible_index)
	_rebuild_submission_list()
	_update_grade_controls()

func _on_submission_selected(index: int) -> void:
	var entries: Array = _visible_entries()
	if index < entries.size():
		_show_entry_detail(entries[index])

func _on_send_grade_pressed() -> void:
	var selected: PackedInt32Array = submission_list.get_selected_items()
	var entries: Array = _visible_entries()
	if selected.is_empty() or selected[0] >= entries.size():
		_set_status("Selecione uma submissao para corrigir")
		return
	var entry: Dictionary = entries[selected[0]]
	var sent: bool = network.send_grade(
		int(entry["room_id"]),
		int(entry["team_index"]),
		float(grade_spin.value),
		comment_line.text.strip_edges()
	)
	if sent:
		comment_line.clear()

func _visible_entries() -> Array:
	var visible: Array = []
	for entry in _entries:
		if _selected_room == -1 or int(entry["room_id"]) == _selected_room:
			visible.append(entry)
	return visible

func _rebuild_room_list() -> void:
	var previous: int = _selected_room
	room_list.clear()
	room_list.add_item("Todas as salas")
	room_list.set_item_metadata(0, -1)
	var room_ids: Array = _rooms.keys()
	room_ids.sort()
	for room_id in room_ids:
		var summary: RoomSummary = _rooms[room_id]
		var label: String = "Sala %d - %d jogador(es)" % [summary.id, summary.player_count]
		if not summary.player_names.is_empty():
			label += ": " + ", ".join(PackedStringArray(summary.player_names))
		room_list.add_item(label)
		room_list.set_item_metadata(room_list.item_count - 1, summary.id)
	var visible_index: int = 0
	for i in room_list.item_count:
		var item_room: int = -1 if i == 0 else int(room_list.get_item_metadata(i))
		if item_room == previous:
			visible_index = i
			break
	if room_list.item_count > 0:
		room_list.select(visible_index)

func _rebuild_submission_list() -> void:
	submission_list.clear()
	for entry in _visible_entries():
		var team: Dictionary = entry["team"]
		submission_list.add_item("Sala %d - Dupla %d (%s) - %s" % [
			int(entry["room_id"]),
			int(entry["team_index"]) + 1,
			_join_ids(team["members"]),
			_format_time(int(team["total_msec"]))
		])
	if submission_list.item_count > 0:
		submission_list.select(0)
		_show_entry_detail(_visible_entries()[0])
	else:
		detail.text = ""
		detail_label.text = "Detalhes da dupla"

func _show_entry_detail(entry: Dictionary) -> void:
	var room_id: int = int(entry["room_id"])
	var team_index: int = int(entry["team_index"])
	var act_id: String = str(entry.get("activity_id", _default_activity_id))
	var team: Dictionary = entry["team"]
	var active_questions: Array = _get_questions_for_activity(act_id)
	var expected_answers: Array = _get_expected_answers_for_activity(act_id)
	detail_label.text = "Detalhes - Sala %d, dupla %d" % [room_id, team_index + 1]

	var lines: PackedStringArray = PackedStringArray()
	lines.append("[b]Integrantes:[/b] %s" % _join_ids(team["members"]))
	lines.append("[b]Tempo total:[/b] %s" % _format_time(int(team["total_msec"])))
	lines.append("")

	var answers: Array = team["answers"]
	var answer_msecs: Array = team["submission_msecs"]
	for i in answers.size():
		lines.append("[b]Questao %d[/b]" % (i + 1))
		if i < active_questions.size():
			lines.append("[color=#8fd8e0]%s[/color]" % _escape_bbcode(str(active_questions[i])))
		if i < expected_answers.size() and not str(expected_answers[i]).is_empty():
			lines.append("[color=#a8e6a1][b]Gabarito esperado:[/b] %s[/color]" % _escape_bbcode(str(expected_answers[i])))
		lines.append("[b]Resposta da dupla:[/b] %s" % _escape_bbcode(str(answers[i])))
		lines.append("Tempo de resposta: %s" % _format_time(int(answer_msecs[i])))
		lines.append("")
	detail.text = "\n".join(lines)

func _update_grade_controls() -> void:
	var selected: bool = not submission_list.get_selected_items().is_empty()
	var connected: bool = network.is_connected_to_server()
	grade_spin.editable = selected
	comment_line.editable = selected
	send_grade_button.disabled = not selected or not connected
	if apply_activity_button != null:
		apply_activity_button.disabled = not connected or _activities.is_empty()

func _populate_activity_option() -> void:
	if activity_option == null:
		return
	activity_option.clear()
	for act in _activities:
		activity_option.add_item(str(act.get("title", act.get("id", ""))))
	if activity_option.item_count > 0:
		activity_option.select(0)

func _get_questions_for_activity(act_id: String) -> Array:
	for act in _activities:
		if str(act.get("id", "")) == act_id:
			return act.get("questions", _questions)
	return _questions

func _get_expected_answers_for_activity(act_id: String) -> Array:
	for act in _activities:
		if str(act.get("id", "")) == act_id:
			return act.get("expected_answers", [])
	return []

func _load_questions() -> Array:
	_activities.clear()
	var file: FileAccess = FileAccess.open(QUIZ_PATH, FileAccess.READ)
	if file == null:
		push_warning("Professor: nao foi possivel ler " + QUIZ_PATH)
		return []
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if parsed is not Dictionary:
		return []
	var dict: Dictionary = parsed as Dictionary
	if dict.has("default_activity"):
		_default_activity_id = str(dict["default_activity"])
	if dict.has("activities") and dict["activities"] is Array:
		for act_item in dict["activities"]:
			if act_item is Dictionary:
				var q_prompts: Array = []
				var q_expected: Array = []
				for q in act_item.get("questions", []):
					if q is Dictionary:
						q_prompts.append(str(q.get("prompt", "")))
						q_expected.append(str(q.get("answer", q.get("word", ""))))
				_activities.append({
					"id": str(act_item.get("id", "")),
					"title": str(act_item.get("title", "")),
					"questions": q_prompts,
					"expected_answers": q_expected
				})
	if not dict.has("questions"):
		return []
	var prompts: Array = []
	for question in dict["questions"]:
		prompts.append(str(question.get("prompt", "")))
	return prompts

# Respostas vem do aluno e podem conter "[" que quebrariam o BBCode do detalhe.
func _escape_bbcode(text: String) -> String:
	return text.replace("[", "[lb]")

func _join_ids(members: Array) -> String:
	var parts: PackedStringArray = PackedStringArray()
	for member in members:
		parts.append(str(member))
	return ", ".join(parts)

func _format_time(msec: int) -> String:
	return "%.1f s" % (float(msec) / 1000.0)
