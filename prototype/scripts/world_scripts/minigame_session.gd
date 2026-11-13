extends Node
class_name MinigameSession

static var is_active: bool = false

const QUIZ_JSON_PATH := "res://prototype/data/minigame_quiz.json"
const REQUIRED_PLAYERS: int = 4

@onready var title_label: Label = get_node_or_null("../Title")
@onready var waiting_label: Label = $"../WaitingLabel"
@onready var document_panel: PanelContainer = $"../DocumentPanel"
@onready var doc_title_label: Label = $"../DocumentPanel/VBox/DocTitleLabel"
@onready var document_text: RichTextLabel = $"../DocumentPanel/VBox/DocumentText"
@onready var quiz_panel: PanelContainer = $"../QuizPanel"
@onready var question_label: Label = $"../QuizPanel/VBox/QuestionLabel"
@onready var answer_line: LineEdit = $"../QuizPanel/VBox/AnswerLine"
@onready var submit_button: Button = $"../QuizPanel/VBox/SubmitButton"
@onready var team_hud: VBoxContainer = $"../TeamHUD"
@onready var timer_label: Label = $"../TeamHUD/TimerLabel"
@onready var progress_label: Label = $"../TeamHUD/ProgressLabel"
@onready var team_info_label: Label = $"../TeamHUD/TeamInfoLabel"

var quiz_data: Dictionary = {}
var activities_catalog: Dictionary = {}
var current_activity_id: String = "defesa_redes"
var total_questions: int = 0
var is_crossword_mode: bool = false
var is_wordsearch_mode: bool = false
var crossword_layout: Dictionary = {}
var wordsearch_board: Dictionary = {}
var team_submitted_answers: Array[String] = []
var ws_clicked_path: Array[Vector2i] = []
var ws_main_split: HBoxContainer = null
var ws_grid: GridContainer = null
var ws_letter_slots_box: HBoxContainer = null
var ws_clear_btn: Button = null
var ws_confirm_btn: Button = null
var ws_checklist_label: RichTextLabel = null
var crossword_quiz_box: VBoxContainer = null
var crossword_quiz_hint: Label = null
var crossword_quiz_grid: GridContainer = null
var crossword_doc_box: VBoxContainer = null
var crossword_doc_hint: Label = null
var crossword_doc_grid: GridContainer = null

var my_team: int = -1
var my_role: int = -1
var my_partner_id: int = -1
var my_team_members: Array[int] = []
var assigned: bool = false

# Estado local da minha dupla (refletido a partir do MinigameProgressPkt).
var my_question_idx: int = 0
var my_finished: bool = false

# Estado autoritativo das duas duplas — só preenchido no host.
# Cada entrada: { members:[id,id], quiz_player_id:int, question_idx:int,
#                 answers:Array[String], submission_msecs:Array[int],
#                 start_msec:int, finished:bool }
# answers/submission_msecs ficarão disponíveis para o passo futuro de envio
# das respostas ao servidor central (para o professor avaliar).
var host_team_state: Array = []

var start_msec: int = 0
var my_grade: float = -1.0
var my_grade_comment: String = ""

func _ready() -> void:
	is_active = true
	if not _load_quiz():
		push_error("Minigame: falha ao carregar %s" % QUIZ_JSON_PATH)
		return

	submit_button.pressed.connect(_on_submit_pressed)
	answer_line.text_submitted.connect(_on_answer_text_submitted)
	answer_line.text_changed.connect(_on_answer_text_changed)
	# Progress chega para todos via broadcast do host; filtro por time no handler.
	PlayerHostPacketHandler.host_minigame_progress_signal.connect(_on_progress_received)
	# Nota: chega via in-game broadcast nos players; o host receberia o lobby
	# loopback porque o seu proprio broadcast nao volta.
	PlayerHostPacketHandler.host_minigame_grade_signal.connect(_on_grade_received)

	if GamePacketHandler.is_host:
		PlayerHostPacketHandler.player_minigame_answer_signal.connect(_on_player_answer_received)
		ClientPacketHandler.host_minigame_grade.connect(_on_grade_loopback)
		ClientPacketHandler.spawn_player_signal.connect(_on_lobby_players_changed)
		GamePacketHandler.peer_connected_to_host.connect(_on_host_peer_connected)
		ClientPacketHandler.activity_changed.connect(_on_activity_changed)
		_try_host_start()
	else:
		ClientPacketHandler.minigame_assigned.connect(_on_assign_signal)
		# O assign pode ter chegado antes desta cena existir. Conferir o buffer.
		if ClientPacketHandler.pending_minigame_assign != null:
			var p: MinigameAssignPkt = ClientPacketHandler.pending_minigame_assign
			ClientPacketHandler.pending_minigame_assign = null
			if not p.activity_id.is_empty():
				_select_activity(p.activity_id)
			_apply_assignment(p.team, p.role, p.partner_id, p.member_ids)

func _exit_tree() -> void:
	is_active = false

func _process(_delta: float) -> void:
	if not assigned or my_finished:
		return
	var elapsed: int = (Time.get_ticks_msec() - start_msec) / 1000
	timer_label.text = "Tempo: %ds" % elapsed

func _load_quiz() -> bool:
	var f: FileAccess = FileAccess.open(QUIZ_JSON_PATH, FileAccess.READ)
	if f == null:
		return false
	var raw: String = f.get_as_text()
	f.close()
	var parsed: Variant = JSON.parse_string(raw)
	if typeof(parsed) != TYPE_DICTIONARY:
		return false
	quiz_data = parsed
	activities_catalog.clear()
	if parsed.has("activities") and typeof(parsed["activities"]) == TYPE_ARRAY:
		for act in parsed["activities"]:
			if typeof(act) == TYPE_DICTIONARY and act.has("id"):
				activities_catalog[String(act["id"])] = act
	var initial_id: String = ClientPacketHandler.current_activity_id
	if initial_id.is_empty() and parsed.has("default_activity"):
		initial_id = String(parsed["default_activity"])
	if not _select_activity(initial_id):
		total_questions = (quiz_data.get("questions", []) as Array).size()
	return true

func _select_activity(act_id: String) -> bool:
	if act_id.is_empty() or not activities_catalog.has(act_id):
		return false
	var act: Dictionary = activities_catalog[act_id]
	current_activity_id = act_id
	ClientPacketHandler.current_activity_id = act_id
	var target_scene: String = String(act.get("scene", ""))
	if not target_scene.is_empty() and get_tree().current_scene != null:
		var curr_path: String = get_tree().current_scene.scene_file_path
		if not curr_path.is_empty() and curr_path != target_scene:
			if GamePacketHandler.is_host and GamePacketHandler.host_connection != null:
				SceneForcePacket.create(target_scene).broadcast(GamePacketHandler.host_connection)
				GameManager.goto_scene(target_scene)
			return true
	quiz_data = act
	total_questions = (quiz_data.get("questions", []) as Array).size()
	var act_type: String = String(act.get("type", ""))
	is_crossword_mode = act_type == "crossword"
	is_wordsearch_mode = act_type == "wordsearch"
	team_submitted_answers.clear()
	ws_clicked_path.clear()
	if is_crossword_mode:
		var gen := CrosswordGenerator.new()
		crossword_layout = gen.generate_from_questions(quiz_data.get("questions", []))
		wordsearch_board = {}
	elif is_wordsearch_mode:
		var ws_gen := WordSearchGenerator.new()
		wordsearch_board = ws_gen.generate_from_questions(quiz_data.get("questions", []))
		crossword_layout = {}
	else:
		crossword_layout = {}
		wordsearch_board = {}
	if title_label != null:
		var mode_prefix: String = "Desafio Individual" if is_wordsearch_mode else "Desafio cooperativo"
		title_label.text = "%s — %s" % [mode_prefix, String(act.get("title", act_id))]
	return true

func _on_lobby_players_changed(_player_id: int) -> void:
	if GamePacketHandler.is_host:
		if is_wordsearch_mode and assigned:
			_host_sync_individual_players()
		else:
			_try_host_start()

func _on_host_peer_connected(_peer: ENetPacketPeer) -> void:
	if GamePacketHandler.is_host:
		if is_wordsearch_mode and assigned:
			_host_sync_individual_players()
		else:
			_try_host_start()

func _on_activity_changed(act_id: String) -> void:
	_select_activity(act_id)
	if GamePacketHandler.is_host:
		assigned = false
		my_finished = false
		my_question_idx = 0
		my_grade = -1.0
		my_grade_comment = ""
		_try_host_start()

func _try_host_start() -> void:
	if assigned:
		return
	var lobby_count: int = ClientPacketHandler.spawned_ids.size()
	var game_peers: int = GamePacketHandler.get_connected_peer_count()
	var min_players: int = 1 if is_wordsearch_mode else REQUIRED_PLAYERS
	var min_peers: int = maxi(0, lobby_count - 1) if is_wordsearch_mode else (REQUIRED_PLAYERS - 1)
	if lobby_count < min_players or game_peers < min_peers:
		document_panel.visible = false
		quiz_panel.visible = false
		team_hud.visible = false
		waiting_label.visible = true
		var act_title: String = String(quiz_data.get("title", quiz_data.get("document_title", current_activity_id)))
		waiting_label.text = "Aguardando jogadores conectarem (%d/%d)...\nTarefa selecionada: %s" % [
			lobby_count,
			min_players,
			act_title
		]
		return
	_host_assign_and_dispatch()

func _host_sync_individual_players() -> void:
	var ids: Array[int] = ClientPacketHandler.spawned_ids.duplicate()
	ids.sort()
	var now: int = Time.get_ticks_msec()
	for player_id in ids:
		var existing_team: int = -1
		for t in host_team_state.size():
			var mems: Array = host_team_state[t]["members"]
			if mems.has(player_id):
				existing_team = t
				break
		if existing_team < 0:
			existing_team = host_team_state.size()
			var mem_arr: Array[int] = [player_id]
			host_team_state.append({
				"members": mem_arr,
				"quiz_player_id": player_id,
				"question_idx": 0,
				"answers": [],
				"submission_msecs": [],
				"start_msec": now,
				"finished": false
			})
		if player_id != ClientPacketHandler.my_id and GamePacketHandler.host_connection != null:
			var single_mems: Array[int] = [player_id]
			MinigameAssignPkt.create(player_id, existing_team, MinigameAssignPkt.ROLE.QUIZ, player_id, single_mems, current_activity_id).broadcast(GamePacketHandler.host_connection)

func _host_assign_and_dispatch() -> void:
	var ids: Array[int] = ClientPacketHandler.spawned_ids.duplicate()
	ids.sort()
	var now: int = Time.get_ticks_msec()
	host_team_state = []

	if is_wordsearch_mode:
		if ids.is_empty():
			return
		for i in ids.size():
			var pid: int = ids[i]
			var single_mems: Array[int] = [pid]
			host_team_state.append({
				"members": single_mems,
				"quiz_player_id": pid,
				"question_idx": 0,
				"answers": [],
				"submission_msecs": [],
				"start_msec": now,
				"finished": false
			})
			if pid == ClientPacketHandler.my_id:
				_apply_assignment(i, MinigameAssignPkt.ROLE.QUIZ, pid, single_mems)
			elif GamePacketHandler.host_connection != null:
				MinigameAssignPkt.create(pid, i, MinigameAssignPkt.ROLE.QUIZ, pid, single_mems, current_activity_id).broadcast(GamePacketHandler.host_connection)
		return

	if ids.size() < REQUIRED_PLAYERS:
		return
	for t in 2:
		var members_t: Array[int] = [ids[t * 2], ids[t * 2 + 1]]
		host_team_state.append({
			"members": members_t,
			"quiz_player_id": ids[t * 2 + 1],
			"question_idx": 0,
			"answers": [],
			"submission_msecs": [],
			"start_msec": now,
			"finished": false
		})

	for i in REQUIRED_PLAYERS:
		var player_id: int = ids[i]
		var team: int = 0 if i < 2 else 1
		var role: int = MinigameAssignPkt.ROLE.DOC if i % 2 == 0 else MinigameAssignPkt.ROLE.QUIZ
		var partner: int = ids[i + 1] if i % 2 == 0 else ids[i - 1]
		var members: Array[int] = [ids[i - i % 2], ids[i - i % 2 + 1]]
		if player_id == ClientPacketHandler.my_id:
			_apply_assignment(team, role, partner, members)
		else:
			MinigameAssignPkt.create(player_id, team, role, partner, members, current_activity_id).broadcast(GamePacketHandler.host_connection)

func _on_assign_signal(pkt: MinigameAssignPkt) -> void:
	ClientPacketHandler.pending_minigame_assign = null
	if not pkt.activity_id.is_empty():
		_select_activity(pkt.activity_id)
	_apply_assignment(pkt.team, pkt.role, pkt.partner_id, pkt.member_ids)

func _apply_assignment(team: int, role: int, partner: int, members: Array[int]) -> void:
	my_team = team
	my_role = role
	my_partner_id = partner
	my_team_members = members
	my_question_idx = 0
	my_finished = false
	my_grade = -1.0
	my_grade_comment = ""
	team_submitted_answers.clear()
	assigned = true
	start_msec = Time.get_ticks_msec()

	# Chat consulta ClientPacketHandler para filtrar/colorir; aqui é o único
	# ponto comum entre host (aplica local) e player (vem do pacote).
	ClientPacketHandler.minigame_team = team
	ClientPacketHandler.minigame_team_members = members.duplicate()

	waiting_label.visible = false
	team_hud.visible = true
	_update_team_info_label()
	_refresh_hud_labels()
	_show_view_for_current_role()

func _show_view_for_current_role() -> void:
	if my_finished:
		_show_finished_state()
		return
	if my_role == MinigameAssignPkt.ROLE.DOC:
		_setup_document_view()
	else:
		_setup_quiz_view()

func _setup_document_view() -> void:
	document_panel.visible = true
	quiz_panel.visible = false
	doc_title_label.text = String(quiz_data.get("document_title", "Documento"))
	document_text.text = String(quiz_data.get("document_text", ""))
	_ensure_crossword_ui()
	if is_crossword_mode:
		document_panel.offset_top = -250.0
		document_panel.offset_bottom = 250.0
		document_text.custom_minimum_size = Vector2(0, 170)
		crossword_doc_box.visible = true
		_rebuild_crossword_boards("")
	elif is_wordsearch_mode:
		document_panel.offset_top = -255.0
		document_panel.offset_bottom = 255.0
		document_text.custom_minimum_size = Vector2(0, 150)
		crossword_doc_box.visible = true
		_rebuild_wordsearch_boards("")
	else:
		document_panel.offset_top = -180.0
		document_panel.offset_bottom = 180.0
		document_text.custom_minimum_size = Vector2(0, 300)
		if crossword_doc_box != null:
			crossword_doc_box.visible = false

func _setup_quiz_view() -> void:
	quiz_panel.visible = true
	document_panel.visible = false
	_ensure_crossword_ui()
	if is_wordsearch_mode:
		quiz_panel.offset_top = -270.0
		quiz_panel.offset_bottom = 270.0
		quiz_panel.offset_left = -520.0
		quiz_panel.offset_right = 520.0
		crossword_quiz_box.visible = true
		if crossword_quiz_grid != null and crossword_quiz_grid.get_parent() is Control:
			(crossword_quiz_grid.get_parent() as Control).visible = false
		if ws_main_split != null:
			ws_main_split.visible = true
	elif is_crossword_mode:
		quiz_panel.offset_top = -255.0
		quiz_panel.offset_bottom = 255.0
		quiz_panel.offset_left = -420.0
		quiz_panel.offset_right = 420.0
		crossword_quiz_box.visible = true
		if crossword_quiz_grid != null and crossword_quiz_grid.get_parent() is Control:
			(crossword_quiz_grid.get_parent() as Control).visible = true
		if ws_main_split != null:
			ws_main_split.visible = false
	else:
		quiz_panel.offset_top = -180.0
		quiz_panel.offset_bottom = 180.0
		quiz_panel.offset_left = -360.0
		quiz_panel.offset_right = 360.0
		if crossword_quiz_box != null:
			crossword_quiz_box.visible = false
	_show_current_question()

func _show_current_question() -> void:
	var questions: Array = quiz_data.get("questions", [])
	if questions.is_empty():
		question_label.text = "(sem perguntas no JSON)"
		return
	if my_finished or my_question_idx >= questions.size():
		_show_finished_state()
		return
	var q: Dictionary = questions[my_question_idx]
	ws_clicked_path.clear()
	if is_crossword_mode:
		var words: Array = crossword_layout.get("words", [])
		var orient: String = "HORIZONTAL"
		var w_len: int = String(q.get("word", q.get("answer", ""))).length()
		if my_question_idx < words.size():
			var w_info: Dictionary = words[my_question_idx]
			orient = "VERTICAL" if bool(w_info.get("vertical", false)) else "HORIZONTAL"
			w_len = String(w_info.get("word", "")).length()
		question_label.text = "Palavra #%d (%s — %d letras): %s" % [
			my_question_idx + 1,
			orient,
			w_len,
			q.get("prompt", "")
		]
		answer_line.max_length = w_len
		answer_line.placeholder_text = "Digite a palavra de %d letras..." % w_len
		answer_line.visible = true
		submit_button.visible = true
		_rebuild_crossword_boards("")
	elif is_wordsearch_mode:
		var ws_words: Array = wordsearch_board.get("words", [])
		var w_len: int = String(q.get("word", q.get("answer", ""))).length()
		if my_question_idx < ws_words.size():
			w_len = String(ws_words[my_question_idx].get("word", "")).length()
		question_label.text = "Missão Atual — Palavra #%d (%d letras): %s" % [
			my_question_idx + 1,
			w_len,
			q.get("prompt", "")
		]
		answer_line.max_length = w_len
		answer_line.visible = false
		submit_button.visible = false
		answer_line.text = ""
		_rebuild_wordsearch_boards("")
		return
	else:
		question_label.text = "%d) %s" % [my_question_idx + 1, q.get("prompt", "")]
		answer_line.max_length = 0
		answer_line.placeholder_text = "Digite sua resposta"
		answer_line.visible = true
		submit_button.visible = true
	answer_line.text = ""
	answer_line.editable = true
	submit_button.disabled = false
	submit_button.visible = true
	submit_button.text = "Enviar"
	answer_line.grab_focus()

func _show_finished_state() -> void:
	quiz_panel.visible = false
	document_panel.visible = false
	waiting_label.visible = true
	if my_grade >= 0.0:
		var prefix: String = "Nota individual" if is_wordsearch_mode else "Nota da dupla"
		var txt: String = "%s: %.1f/10" % [prefix, my_grade]
		if not my_grade_comment.is_empty():
			txt += "\n\nComentario do professor:\n" + my_grade_comment
		waiting_label.text = txt
	else:
		waiting_label.text = "Caça-Palavras concluído! Aguarde o professor enviar sua nota." if is_wordsearch_mode else "Respostas enviadas. Aguarde o professor dar nota."

func _on_grade_received(data: PackedByteArray) -> void:
	var pkt: MinigameGradeResultPkt = MinigameGradeResultPkt.create_from_data(data)
	_apply_grade(pkt.team_index, pkt.grade, pkt.comment)

func _on_grade_loopback(team_index: int, grade: float, comment: String) -> void:
	_apply_grade(team_index, grade, comment)

func _apply_grade(team_index: int, grade: float, comment: String) -> void:
	if team_index != my_team:
		return
	# Idempotente: aplicar de novo so atualiza o texto.
	my_grade = grade
	my_grade_comment = comment
	if my_finished:
		_show_finished_state()

func _on_answer_text_submitted(_text: String) -> void:
	_on_submit_pressed()

func _on_answer_text_changed(new_text: String) -> void:
	if my_role != MinigameAssignPkt.ROLE.QUIZ:
		return
	if is_crossword_mode:
		_rebuild_crossword_boards(new_text)
	elif is_wordsearch_mode:
		_rebuild_wordsearch_boards(new_text)

func _on_submit_pressed() -> void:
	if not assigned or my_finished:
		return
	if my_role != MinigameAssignPkt.ROLE.QUIZ:
		return
	var raw: String = answer_line.text
	if raw.strip_edges().is_empty():
		return
	# Trava o input até a próxima atribuição de papel chegar do host.
	submit_button.disabled = true
	answer_line.editable = false
	if GamePacketHandler.is_host:
		_host_process_answer(my_team, my_question_idx, raw)
	else:
		MinigameAnswerPkt.create(my_team, my_question_idx, raw).send(GamePacketHandler.host_peer)

func _on_player_answer_received(data: PackedByteArray) -> void:
	# Só roda no host.
	var pkt: MinigameAnswerPkt = MinigameAnswerPkt.create_from_data(data)
	_host_process_answer(pkt.team, pkt.question_idx, pkt.answer)

func _host_process_answer(team: int, question_idx: int, raw_answer: String) -> void:
	if team < 0 or team >= host_team_state.size():
		return
	var state: Dictionary = host_team_state[team]
	if bool(state["finished"]):
		return
	# Garante que estamos respondendo a pergunta corrente (drop em duplicatas/atrasos).
	if question_idx != int(state["question_idx"]):
		return
	var questions: Array = quiz_data.get("questions", [])
	if question_idx < 0 or question_idx >= questions.size():
		return

	# Sem auto-correção: armazena a resposta e o tempo desde o início da dupla.
	state["answers"].append(raw_answer)
	state["submission_msecs"].append(Time.get_ticks_msec() - int(state["start_msec"]))
	state["question_idx"] = int(state["question_idx"]) + 1

	# Inverte o papel: quem respondeu vira DOC; o parceiro assume o QUIZ.
	var members: Array = state["members"]
	var prev_quiz: int = int(state["quiz_player_id"])
	var next_quiz: int = prev_quiz
	for m in members:
		if int(m) != prev_quiz:
			next_quiz = int(m)
			break
	state["quiz_player_id"] = next_quiz

	var finished: bool = int(state["question_idx"]) >= questions.size()
	state["finished"] = finished

	MinigameProgressPkt.create(team, int(state["question_idx"]), next_quiz, finished, raw_answer).broadcast(GamePacketHandler.host_connection)
	# Broadcast não volta ao próprio host; aplica localmente se for o meu time.
	if team == my_team:
		_apply_progress(team, int(state["question_idx"]), next_quiz, finished, raw_answer)

	# Quando as duas duplas concluíram, host envia tudo ao servidor central
	# para o professor avaliar. Único envio por sala (acordado: batch ao final).
	if finished and _all_teams_finished():
		_host_submit_to_central()

func _all_teams_finished() -> bool:
	for state in host_team_state:
		if not bool(state["finished"]):
			return false
	return true

func _host_submit_to_central() -> void:
	if ClientNet.server_peer == null:
		push_error("Minigame: host sem conexão com servidor central; submissão perdida.")
		return
	var now: int = Time.get_ticks_msec()
	var teams_payload: Array = []
	for state in host_team_state:
		teams_payload.append({
			"members": (state["members"] as Array).duplicate(),
			"total_msec": now - int(state["start_msec"]),
			"answers": (state["answers"] as Array).duplicate(),
			"submission_msecs": (state["submission_msecs"] as Array).duplicate()
		})
	MinigameSubmissionPkt.create(ClientPacketHandler.current_room_id, teams_payload).send(ClientNet.server_peer)

func _on_progress_received(data: PackedByteArray) -> void:
	var pkt: MinigameProgressPkt = MinigameProgressPkt.create_from_data(data)
	if pkt.team != my_team:
		return
	_apply_progress(pkt.team, pkt.question_idx, pkt.quiz_player_id, pkt.finished != 0, pkt.last_answer)

func _apply_progress(_team: int, question_idx: int, quiz_player_id: int, finished: bool, last_answer: String = "") -> void:
	if not last_answer.is_empty():
		team_submitted_answers.append(CrosswordGenerator.normalize_word(last_answer))
	my_question_idx = question_idx
	my_finished = finished
	# Meu papel a partir de agora é definido pela identidade do QUIZ atual.
	my_role = MinigameAssignPkt.ROLE.QUIZ if quiz_player_id == ClientPacketHandler.my_id else MinigameAssignPkt.ROLE.DOC
	_update_team_info_label()
	_refresh_hud_labels()
	_show_view_for_current_role()

func _ensure_crossword_ui() -> void:
	if crossword_quiz_box == null:
		var q_vbox: VBoxContainer = $"../QuizPanel/VBox"
		crossword_quiz_box = VBoxContainer.new()
		crossword_quiz_box.name = "CrosswordQuizBox"
		crossword_quiz_box.add_theme_constant_override("separation", 6)
		crossword_quiz_hint = Label.new()
		crossword_quiz_hint.text = "Tabuleiro da Cruzadinha (células azuis = palavra da vez; verdes = já preenchidas)"
		crossword_quiz_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		crossword_quiz_hint.add_theme_font_size_override("font_size", 12)
		crossword_quiz_box.add_child(crossword_quiz_hint)
		var center := CenterContainer.new()
		center.name = "CrosswordCenter"
		crossword_quiz_grid = GridContainer.new()
		crossword_quiz_grid.add_theme_constant_override("h_separation", 2)
		crossword_quiz_grid.add_theme_constant_override("v_separation", 2)
		center.add_child(crossword_quiz_grid)
		crossword_quiz_box.add_child(center)

		ws_main_split = HBoxContainer.new()
		ws_main_split.name = "WordSearchMainSplit"
		ws_main_split.alignment = BoxContainer.ALIGNMENT_CENTER
		ws_main_split.visible = false
		ws_main_split.add_theme_constant_override("separation", 18)

		var left_vbox := VBoxContainer.new()
		left_vbox.alignment = BoxContainer.ALIGNMENT_CENTER
		left_vbox.add_theme_constant_override("separation", 8)

		var ws_grid_center := CenterContainer.new()
		ws_grid = GridContainer.new()
		ws_grid.add_theme_constant_override("h_separation", 3)
		ws_grid.add_theme_constant_override("v_separation", 3)
		ws_grid_center.add_child(ws_grid)
		left_vbox.add_child(ws_grid_center)

		ws_letter_slots_box = HBoxContainer.new()
		ws_letter_slots_box.alignment = BoxContainer.ALIGNMENT_CENTER
		ws_letter_slots_box.add_theme_constant_override("separation", 4)
		left_vbox.add_child(ws_letter_slots_box)

		var btn_row := HBoxContainer.new()
		btn_row.alignment = BoxContainer.ALIGNMENT_CENTER
		btn_row.add_theme_constant_override("separation", 10)

		ws_clear_btn = Button.new()
		ws_clear_btn.text = "⌫ Limpar Seleção"
		ws_clear_btn.custom_minimum_size = Vector2(145, 32)
		ws_clear_btn.pressed.connect(_on_wordsearch_clear_pressed)
		btn_row.add_child(ws_clear_btn)

		ws_confirm_btn = Button.new()
		ws_confirm_btn.text = "✔ Confirmar Palavra"
		ws_confirm_btn.custom_minimum_size = Vector2(165, 32)
		ws_confirm_btn.pressed.connect(_on_wordsearch_confirm_pressed)
		btn_row.add_child(ws_confirm_btn)

		left_vbox.add_child(btn_row)
		ws_main_split.add_child(left_vbox)

		var right_panel := PanelContainer.new()
		right_panel.custom_minimum_size = Vector2(360, 340)
		var right_style := StyleBoxFlat.new()
		right_style.bg_color = Color(0.09, 0.11, 0.17, 0.95)
		right_style.border_color = Color(0.28, 0.36, 0.52, 1.0)
		right_style.set_border_width_all(1)
		right_style.set_content_margin_all(10)
		right_panel.add_theme_stylebox_override("panel", right_style)

		var right_vbox := VBoxContainer.new()
		right_vbox.add_theme_constant_override("separation", 6)
		var right_title := Label.new()
		right_title.text = "Painel de Missões — Termos Cyber"
		right_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		right_title.add_theme_font_size_override("font_size", 13)
		right_title.add_theme_color_override("font_color", Color(0.55, 0.85, 1.0, 1.0))
		right_vbox.add_child(right_title)

		ws_checklist_label = RichTextLabel.new()
		ws_checklist_label.bbcode_enabled = true
		ws_checklist_label.fit_content = false
		ws_checklist_label.scroll_active = true
		ws_checklist_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
		ws_checklist_label.custom_minimum_size = Vector2(335, 290)
		ws_checklist_label.add_theme_font_size_override("normal_font_size", 12)
		ws_checklist_label.add_theme_font_size_override("bold_font_size", 12)
		right_vbox.add_child(ws_checklist_label)
		right_panel.add_child(right_vbox)

		ws_main_split.add_child(right_panel)
		crossword_quiz_box.add_child(ws_main_split)

		q_vbox.add_child(crossword_quiz_box)
		q_vbox.move_child(crossword_quiz_box, 0)

	if crossword_doc_box == null:
		var d_vbox: VBoxContainer = $"../DocumentPanel/VBox"
		crossword_doc_box = VBoxContainer.new()
		crossword_doc_box.name = "CrosswordDocBox"
		crossword_doc_hint = Label.new()
		crossword_doc_hint.text = "Progresso da Dupla na Cruzadinha (acompanhe os cruzamentos do seu parceiro)"
		crossword_doc_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		crossword_doc_hint.add_theme_font_size_override("font_size", 12)
		crossword_doc_box.add_child(crossword_doc_hint)
		var center_doc := CenterContainer.new()
		crossword_doc_grid = GridContainer.new()
		crossword_doc_grid.add_theme_constant_override("h_separation", 2)
		crossword_doc_grid.add_theme_constant_override("v_separation", 2)
		center_doc.add_child(crossword_doc_grid)
		crossword_doc_box.add_child(center_doc)
		d_vbox.add_child(crossword_doc_box)

func _rebuild_crossword_boards(preview_typing: String = "") -> void:
	if not is_crossword_mode or crossword_layout.is_empty():
		return
	_ensure_crossword_ui()
	var target_grid: GridContainer = crossword_quiz_grid if my_role == MinigameAssignPkt.ROLE.QUIZ else crossword_doc_grid
	if target_grid == null:
		return

	for child in target_grid.get_children():
		child.queue_free()

	var rows_cnt: int = int(crossword_layout.get("rows", 1))
	var cols_cnt: int = int(crossword_layout.get("cols", 1))
	var words: Array = crossword_layout.get("words", [])
	target_grid.columns = cols_cnt

	# Mapa de células: chave "r_c" -> { "slot": bool, "active": bool, "filled": bool, "letter": String, "num": int }
	var cell_map: Dictionary = {}
	for w_idx in words.size():
		var w_info: Dictionary = words[w_idx]
		var wr: int = int(w_info["row"])
		var wc: int = int(w_info["col"])
		var wvert: bool = bool(w_info["vertical"])
		var wlen: int = String(w_info["word"]).length()
		var wnum: int = int(w_info["number"])
		var submitted_str: String = team_submitted_answers[w_idx] if w_idx < team_submitted_answers.size() else ""

		for i in wlen:
			var r: int = wr + (i if wvert else 0)
			var c: int = wc + (0 if wvert else i)
			var key: String = "%d_%d" % [r, c]
			var entry: Dictionary = cell_map.get(key, {
				"slot": true,
				"active": false,
				"filled": false,
				"letter": "",
				"num": 0
			})
			if i == 0 and int(entry["num"]) == 0:
				entry["num"] = wnum
			if not submitted_str.is_empty() and i < submitted_str.length():
				entry["letter"] = submitted_str.substr(i, 1)
				entry["filled"] = true
			if w_idx == my_question_idx and not my_finished:
				entry["active"] = true
			cell_map[key] = entry

	# Aplica preview em tempo real do que o jogador QUIZ está digitando
	var clean_preview: String = CrosswordGenerator.normalize_word(preview_typing)
	if not clean_preview.is_empty() and my_question_idx < words.size() and not my_finished:
		var cur_w: Dictionary = words[my_question_idx]
		var cr: int = int(cur_w["row"])
		var cc: int = int(cur_w["col"])
		var cvert: bool = bool(cur_w["vertical"])
		var clen: int = String(cur_w["word"]).length()
		for i in min(clean_preview.length(), clen):
			var r: int = cr + (i if cvert else 0)
			var c: int = cc + (0 if cvert else i)
			var key: String = "%d_%d" % [r, c]
			if cell_map.has(key):
				cell_map[key]["letter"] = clean_preview.substr(i, 1)

	var cell_px: int = clampi(int(260.0 / max(rows_cnt, 1)), 20, 28)
	for r in rows_cnt:
		for c in cols_cnt:
			var key: String = "%d_%d" % [r, c]
			var cell := Panel.new()
			cell.custom_minimum_size = Vector2(cell_px, cell_px)
			var style := StyleBoxFlat.new()
			if not cell_map.has(key):
				style.bg_color = Color(0.08, 0.09, 0.14, 0.25)
				cell.add_theme_stylebox_override("panel", style)
				target_grid.add_child(cell)
				continue

			var info: Dictionary = cell_map[key]
			style.set_border_width_all(1)
			if bool(info["active"]):
				style.bg_color = Color(0.16, 0.38, 0.54, 1.0)
				style.border_color = Color(0.45, 0.85, 1.0, 1.0)
			elif bool(info["filled"]):
				style.bg_color = Color(0.15, 0.33, 0.24, 1.0)
				style.border_color = Color(0.35, 0.75, 0.5, 1.0)
			else:
				style.bg_color = Color(0.2, 0.22, 0.32, 1.0)
				style.border_color = Color(0.4, 0.44, 0.58, 1.0)
			cell.add_theme_stylebox_override("panel", style)

			if int(info["num"]) > 0:
				var num_lbl := Label.new()
				num_lbl.text = str(info["num"])
				num_lbl.position = Vector2(2, 0)
				num_lbl.add_theme_font_size_override("font_size", 8)
				num_lbl.add_theme_color_override("font_color", Color(0.95, 0.85, 0.4, 1.0))
				cell.add_child(num_lbl)

			var ch_lbl := Label.new()
			ch_lbl.text = String(info["letter"])
			ch_lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
			ch_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			ch_lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			ch_lbl.add_theme_font_size_override("font_size", 12)
			cell.add_child(ch_lbl)

			target_grid.add_child(cell)

func _get_formed_wordsearch_string() -> String:
	var grid: Array = wordsearch_board.get("grid", [])
	if grid.is_empty() or ws_clicked_path.is_empty():
		return ""
	var built: String = ""
	for pos in ws_clicked_path:
		if pos.x >= 0 and pos.x < grid.size() and pos.y >= 0 and pos.y < (grid[pos.x] as Array).size():
			built += String(grid[pos.x][pos.y])
	return built

func _on_wordsearch_clear_pressed() -> void:
	ws_clicked_path.clear()
	answer_line.text = ""
	_rebuild_wordsearch_boards("")

func _on_wordsearch_confirm_pressed() -> void:
	var formed: String = _get_formed_wordsearch_string()
	if formed.strip_edges().is_empty():
		return
	answer_line.text = formed
	_on_submit_pressed()

func _rebuild_wordsearch_boards(_preview_typing: String = "") -> void:
	if not is_wordsearch_mode or wordsearch_board.is_empty():
		return
	_ensure_crossword_ui()
	var words: Array = wordsearch_board.get("words", [])
	var grid: Array = wordsearch_board.get("grid", [])
	var questions: Array = quiz_data.get("questions", [])

	var target_len: int = 0
	if my_question_idx < words.size() and not my_finished:
		target_len = String(words[my_question_idx].get("word", "")).length()

	var formed_word: String = _get_formed_wordsearch_string()
	answer_line.text = formed_word

	if crossword_quiz_hint != null:
		if ws_clicked_path.is_empty():
			crossword_quiz_hint.text = "Modo Individual: clique nos quadrados para formar a Palavra #%d (%d letras)!" % [my_question_idx + 1, target_len]
		elif ws_clicked_path.size() < target_len:
			crossword_quiz_hint.text = "Formando: \"%s\" (%d/%d letras) — continue clicando nos quadrados em linha reta!" % [formed_word, ws_clicked_path.size(), target_len]
		else:
			crossword_quiz_hint.text = "Palavra formada: \"%s\" (%d letras) — clique em [✔ Confirmar Palavra]!" % [formed_word, ws_clicked_path.size()]

	if ws_grid == null:
		return
	for child in ws_grid.get_children():
		child.queue_free()

	var rows_cnt: int = int(wordsearch_board.get("rows", 12))
	var cols_cnt: int = int(wordsearch_board.get("cols", 12))
	ws_grid.columns = cols_cnt

	var found_cells: Dictionary = {}
	var start_nums: Dictionary = {}
	for w_idx in min(team_submitted_answers.size(), words.size()):
		var w_info: Dictionary = words[w_idx]
		var sr: int = int(w_info["start_row"])
		var sc: int = int(w_info["start_col"])
		var dr: int = int(w_info["dr"])
		var dc: int = int(w_info["dc"])
		var wlen: int = String(w_info["word"]).length()
		var s_key: String = "%d_%d" % [sr, sc]
		if not start_nums.has(s_key):
			start_nums[s_key] = int(w_info["number"])
		for i in wlen:
			found_cells["%d_%d" % [sr + dr * i, sc + dc * i]] = true

	var clicked_index_map: Dictionary = {}
	for i in ws_clicked_path.size():
		var p: Vector2i = ws_clicked_path[i]
		clicked_index_map["%d_%d" % [p.x, p.y]] = i + 1

	var cell_px: int = 27
	for r in rows_cnt:
		for c in cols_cnt:
			var key: String = "%d_%d" % [r, c]
			var cell := Panel.new()
			cell.custom_minimum_size = Vector2(cell_px, cell_px)
			cell.mouse_filter = Control.MOUSE_FILTER_STOP
			cell.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
			var style := StyleBoxFlat.new()
			style.set_border_width_all(1)
			style.set_corner_radius_all(3)

			var click_order: int = int(clicked_index_map.get(key, 0))
			var is_sel: bool = click_order > 0
			var is_last_clicked: bool = is_sel and click_order == ws_clicked_path.size()
			var is_found: bool = found_cells.has(key)

			if is_last_clicked:
				style.bg_color = Color(0.22, 0.56, 0.78, 1.0)
				style.border_color = Color(1.0, 0.92, 0.45, 1.0)
				style.set_border_width_all(2)
			elif is_sel:
				style.bg_color = Color(0.16, 0.44, 0.65, 1.0)
				style.border_color = Color(0.48, 0.9, 1.0, 1.0)
			elif is_found:
				style.bg_color = Color(0.14, 0.36, 0.24, 1.0)
				style.border_color = Color(0.35, 0.82, 0.52, 1.0)
			else:
				style.bg_color = Color(0.16, 0.19, 0.27, 1.0)
				style.border_color = Color(0.3, 0.35, 0.48, 1.0)
			cell.add_theme_stylebox_override("panel", style)

			if is_sel:
				var order_lbl := Label.new()
				order_lbl.text = str(click_order)
				order_lbl.position = Vector2(2, -1)
				order_lbl.add_theme_font_size_override("font_size", 8)
				order_lbl.add_theme_color_override("font_color", Color(1.0, 0.92, 0.4, 1.0))
				cell.add_child(order_lbl)
			elif int(start_nums.get(key, 0)) > 0:
				var num_lbl := Label.new()
				num_lbl.text = str(start_nums[key])
				num_lbl.position = Vector2(2, -1)
				num_lbl.add_theme_font_size_override("font_size", 8)
				num_lbl.add_theme_color_override("font_color", Color(0.55, 0.95, 0.68, 1.0))
				cell.add_child(num_lbl)

			var ch_lbl := Label.new()
			ch_lbl.text = String(grid[r][c])
			ch_lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
			ch_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			ch_lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			ch_lbl.add_theme_font_size_override("font_size", 13)
			if is_sel:
				ch_lbl.add_theme_color_override("font_color", Color(1.0, 1.0, 1.0, 1.0))
			cell.add_child(ch_lbl)

			if not my_finished:
				cell.gui_input.connect(_on_wordsearch_cell_gui_input.bind(r, c))

			ws_grid.add_child(cell)

	if ws_letter_slots_box != null:
		for child in ws_letter_slots_box.get_children():
			child.queue_free()
		var slots_count: int = maxi(target_len, ws_clicked_path.size())
		for i in slots_count:
			var has_letter: bool = i < formed_word.length()
			var slot_panel := Panel.new()
			slot_panel.custom_minimum_size = Vector2(28, 30)
			var slot_style := StyleBoxFlat.new()
			slot_style.set_border_width_all(1)
			slot_style.set_corner_radius_all(4)
			if has_letter:
				slot_style.bg_color = Color(0.18, 0.45, 0.66, 1.0)
				slot_style.border_color = Color(0.55, 0.92, 1.0, 1.0)
			else:
				slot_style.bg_color = Color(0.11, 0.13, 0.19, 1.0)
				slot_style.border_color = Color(0.32, 0.38, 0.52, 1.0)
			slot_panel.add_theme_stylebox_override("panel", slot_style)

			var slot_lbl := Label.new()
			slot_lbl.text = formed_word.substr(i, 1) if has_letter else "_"
			slot_lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
			slot_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			slot_lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			slot_lbl.add_theme_font_size_override("font_size", 14)
			slot_panel.add_child(slot_lbl)
			ws_letter_slots_box.add_child(slot_panel)

	if ws_clear_btn != null:
		ws_clear_btn.disabled = ws_clicked_path.is_empty() or my_finished
	if ws_confirm_btn != null:
		ws_confirm_btn.disabled = ws_clicked_path.is_empty() or my_finished
		ws_confirm_btn.text = "✔ Confirmar (%d/%d)" % [ws_clicked_path.size(), target_len] if target_len > 0 else "✔ Confirmar Palavra"

	if ws_checklist_label != null:
		var lines: PackedStringArray = PackedStringArray()
		for i in words.size():
			var w_info: Dictionary = words[i]
			var q_prompt: String = ""
			if i < questions.size() and questions[i] is Dictionary:
				q_prompt = String(questions[i].get("prompt", ""))
			var w_word: String = String(w_info.get("word", ""))
			var w_dir: String = String(w_info.get("direction_name", ""))
			if i < my_question_idx:
				var ans: String = team_submitted_answers[i] if i < team_submitted_answers.size() else w_word
				lines.append("[color=#6ee787][b]✔ #%d: %s[/b] (%s)[/color]\n" % [i + 1, ans, w_dir])
			elif i == my_question_idx and not my_finished:
				lines.append("[color=#58d5ff][b]▶ #%d (%d letras — %s):[/b]\n%s[/color]\n" % [i + 1, w_word.length(), w_dir, q_prompt])
			else:
				lines.append("[color=#8b95a8]• #%d (%d letras): %s[/color]\n" % [i + 1, w_word.length(), q_prompt])
		ws_checklist_label.text = "\n".join(lines)

func _on_wordsearch_cell_gui_input(event: InputEvent, r: int, c: int) -> void:
	if not (event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT):
		return
	if not is_wordsearch_mode or my_finished:
		return

	var words: Array = wordsearch_board.get("words", [])
	var target_len: int = String(words[my_question_idx].get("word", "")).length() if my_question_idx < words.size() else 12
	var clicked_pos := Vector2i(r, c)

	if ws_clicked_path.is_empty():
		ws_clicked_path.append(clicked_pos)
		_rebuild_wordsearch_boards("")
		return

	if ws_clicked_path[ws_clicked_path.size() - 1] == clicked_pos:
		ws_clicked_path.pop_back()
		_rebuild_wordsearch_boards("")
		return

	var existing_idx: int = ws_clicked_path.find(clicked_pos)
	if existing_idx >= 0:
		ws_clicked_path.resize(existing_idx + 1)
		_rebuild_wordsearch_boards("")
		return

	var start: Vector2i = ws_clicked_path[0]
	var dr_total: int = r - start.x
	var dc_total: int = c - start.y
	var abs_r: int = absi(dr_total)
	var abs_c: int = absi(dc_total)

	if dr_total == 0 or dc_total == 0 or abs_r == abs_c:
		var step_r: int = signi(dr_total)
		var step_c: int = signi(dc_total)
		var same_dir: bool = true
		if ws_clicked_path.size() >= 2:
			var cur_dir_r: int = ws_clicked_path[1].x - start.x
			var cur_dir_c: int = ws_clicked_path[1].y - start.y
			if step_r != cur_dir_r or step_c != cur_dir_c:
				same_dir = false
		var steps: int = maxi(abs_r, abs_c)
		if same_dir and steps + 1 <= target_len:
			ws_clicked_path.clear()
			for i in range(steps + 1):
				ws_clicked_path.append(Vector2i(start.x + step_r * i, start.y + step_c * i))
			_rebuild_wordsearch_boards("")
			return

	ws_clicked_path.clear()
	ws_clicked_path.append(clicked_pos)
	_rebuild_wordsearch_boards("")

func _update_team_info_label() -> void:
	if my_team < 0:
		return
	if is_wordsearch_mode:
		team_info_label.text = "Modo Individual — Aluno: player_%d | Encontradas: %d/%d" % [
			ClientPacketHandler.my_id,
			my_question_idx,
			total_questions
		]
		return
	var my_role_text: String = "QUIZ" if my_role == MinigameAssignPkt.ROLE.QUIZ else "DOC"
	var partner_role_text: String = "DOC" if my_role == MinigameAssignPkt.ROLE.QUIZ else "QUIZ"
	team_info_label.text = "Dupla %s — você: %s | parceiro: player_%d (%s)" % [
		"A" if my_team == 0 else "B",
		my_role_text,
		my_partner_id,
		partner_role_text
	]

func _refresh_hud_labels() -> void:
	if my_finished:
		progress_label.text = "Concluído (%d/%d)" % [total_questions, total_questions]
	elif is_wordsearch_mode:
		progress_label.text = "Palavra %d de %d" % [my_question_idx + 1, total_questions]
	else:
		progress_label.text = "Pergunta %d de %d" % [my_question_idx + 1, total_questions]
