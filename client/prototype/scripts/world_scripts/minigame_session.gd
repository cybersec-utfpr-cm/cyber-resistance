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
var crossword_layout: Dictionary = {}
var team_submitted_answers: Array[String] = []
var crossword_quiz_box: VBoxContainer = null
var crossword_quiz_grid: GridContainer = null
var crossword_doc_box: VBoxContainer = null
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
	is_crossword_mode = String(act.get("type", "")) == "crossword"
	team_submitted_answers.clear()
	if is_crossword_mode:
		var gen := CrosswordGenerator.new()
		crossword_layout = gen.generate_from_questions(quiz_data.get("questions", []))
	else:
		crossword_layout = {}
	if title_label != null:
		title_label.text = "Desafio cooperativo — %s" % String(act.get("title", act_id))
	return true

func _on_lobby_players_changed(_player_id: int) -> void:
	if GamePacketHandler.is_host:
		_try_host_start()

func _on_host_peer_connected(_peer: ENetPacketPeer) -> void:
	if GamePacketHandler.is_host:
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
	if lobby_count < REQUIRED_PLAYERS or game_peers < REQUIRED_PLAYERS - 1:
		document_panel.visible = false
		quiz_panel.visible = false
		team_hud.visible = false
		waiting_label.visible = true
		var act_title: String = String(quiz_data.get("title", quiz_data.get("document_title", current_activity_id)))
		waiting_label.text = "Aguardando jogadores conectarem (%d/%d)...\nTarefa selecionada: %s" % [
			lobby_count,
			REQUIRED_PLAYERS,
			act_title
		]
		return
	_host_assign_and_dispatch()

func _host_assign_and_dispatch() -> void:
	var ids: Array[int] = ClientPacketHandler.spawned_ids.duplicate()
	if ids.size() < REQUIRED_PLAYERS:
		return
	ids.sort()
	# Dupla A: ids[0] DOC, ids[1] QUIZ — Dupla B: ids[2] DOC, ids[3] QUIZ.
	# Determinístico via sort para que host e clientes derivem a mesma partição.

	# Inicializa o estado por dupla antes de aplicar/disparar os assigns para
	# que qualquer resposta que chegar logo em seguida ache estado pronto.
	var now: int = Time.get_ticks_msec()
	host_team_state = []
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
			# Broadcast do ENet não volta ao remetente; aplicamos localmente.
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
	if is_crossword_mode:
		quiz_panel.offset_top = -250.0
		quiz_panel.offset_bottom = 250.0
		quiz_panel.offset_left = -420.0
		quiz_panel.offset_right = 420.0
		crossword_quiz_box.visible = true
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
		_rebuild_crossword_boards("")
	else:
		question_label.text = "%d) %s" % [my_question_idx + 1, q.get("prompt", "")]
		answer_line.max_length = 0
		answer_line.placeholder_text = "Digite sua resposta"
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
		var txt: String = "Nota da dupla: %.1f/10" % my_grade
		if not my_grade_comment.is_empty():
			txt += "\n\nComentario do professor:\n" + my_grade_comment
		waiting_label.text = txt
	else:
		waiting_label.text = "Respostas enviadas. Aguarde o professor dar nota."

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
	if is_crossword_mode and my_role == MinigameAssignPkt.ROLE.QUIZ:
		_rebuild_crossword_boards(new_text)

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
	if ProtNetworkHandler.server_peer == null:
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
	MinigameSubmissionPkt.create(ClientPacketHandler.current_room_id, teams_payload).send(ProtNetworkHandler.server_peer)

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
		var hint_lbl := Label.new()
		hint_lbl.text = "Tabuleiro da Cruzadinha (células azuis = palavra da vez; verdes = já preenchidas)"
		hint_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		hint_lbl.add_theme_font_size_override("font_size", 12)
		crossword_quiz_box.add_child(hint_lbl)
		var center := CenterContainer.new()
		crossword_quiz_grid = GridContainer.new()
		crossword_quiz_grid.add_theme_constant_override("h_separation", 2)
		crossword_quiz_grid.add_theme_constant_override("v_separation", 2)
		center.add_child(crossword_quiz_grid)
		crossword_quiz_box.add_child(center)
		q_vbox.add_child(crossword_quiz_box)
		q_vbox.move_child(crossword_quiz_box, 0)

	if crossword_doc_box == null:
		var d_vbox: VBoxContainer = $"../DocumentPanel/VBox"
		crossword_doc_box = VBoxContainer.new()
		crossword_doc_box.name = "CrosswordDocBox"
		var doc_hint := Label.new()
		doc_hint.text = "Progresso da Dupla na Cruzadinha (acompanhe os cruzamentos do seu parceiro)"
		doc_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		doc_hint.add_theme_font_size_override("font_size", 12)
		crossword_doc_box.add_child(doc_hint)
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

func _update_team_info_label() -> void:
	if my_team < 0:
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
	else:
		progress_label.text = "Pergunta %d de %d" % [my_question_idx + 1, total_questions]
