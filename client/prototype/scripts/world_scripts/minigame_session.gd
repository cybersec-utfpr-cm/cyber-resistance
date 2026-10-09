extends Node
class_name MinigameSession

static var is_active: bool = false

const QUIZ_JSON_PATH := "res://prototype/data/minigame_quiz.json"

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
var total_questions: int = 0

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
	# Progress chega para todos via broadcast do host; filtro por time no handler.
	PlayerHostPacketHandler.host_minigame_progress_signal.connect(_on_progress_received)
	# Nota: chega via in-game broadcast nos players; o host receberia o lobby
	# loopback porque o seu proprio broadcast nao volta.
	PlayerHostPacketHandler.host_minigame_grade_signal.connect(_on_grade_received)

	if GamePacketHandler.is_host:
		PlayerHostPacketHandler.player_minigame_answer_signal.connect(_on_player_answer_received)
		ClientPacketHandler.host_minigame_grade.connect(_on_grade_loopback)
		# Conhece os ids da partida e calcula tudo localmente; não fica esperando
		# seu próprio pacote pelo loopback (broadcast não volta ao remetente).
		_host_assign_and_dispatch()
	else:
		# O assign chega no broadcast quase junto com o SceneForcePacket que nos
		# trouxe; pode ter chegado antes desta cena existir. Conferir o buffer.
		if ClientPacketHandler.pending_minigame_assign != null:
			var p: MinigameAssignPkt = ClientPacketHandler.pending_minigame_assign
			ClientPacketHandler.pending_minigame_assign = null
			_apply_assignment(p.team, p.role, p.partner_id, p.member_ids)
		else:
			ClientPacketHandler.minigame_assigned.connect(_on_assign_signal)

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
	total_questions = (quiz_data.get("questions", []) as Array).size()
	return true

func _host_assign_and_dispatch() -> void:
	var ids: Array[int] = ClientPacketHandler.spawned_ids.duplicate()
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

	for i in ids.size():
		var player_id: int = ids[i]
		var team: int = 0 if i < 2 else 1
		var role: int = MinigameAssignPkt.ROLE.DOC if i % 2 == 0 else MinigameAssignPkt.ROLE.QUIZ
		var partner: int = ids[i + 1] if i % 2 == 0 else ids[i - 1]
		var members: Array[int] = [ids[i - i % 2], ids[i - i % 2 + 1]]
		if player_id == ClientPacketHandler.my_id:
			# Broadcast do ENet não volta ao remetente; aplicamos localmente.
			_apply_assignment(team, role, partner, members)
		else:
			MinigameAssignPkt.create(player_id, team, role, partner, members).broadcast(GamePacketHandler.host_connection)

func _on_assign_signal(pkt: MinigameAssignPkt) -> void:
	ClientPacketHandler.pending_minigame_assign = null
	_apply_assignment(pkt.team, pkt.role, pkt.partner_id, pkt.member_ids)

func _apply_assignment(team: int, role: int, partner: int, members: Array[int]) -> void:
	my_team = team
	my_role = role
	my_partner_id = partner
	my_team_members = members
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

func _setup_quiz_view() -> void:
	quiz_panel.visible = true
	document_panel.visible = false
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
	question_label.text = "%d) %s" % [my_question_idx + 1, q.get("prompt", "")]
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

	MinigameProgressPkt.create(team, int(state["question_idx"]), next_quiz, finished).broadcast(GamePacketHandler.host_connection)
	# Broadcast não volta ao próprio host; aplica localmente se for o meu time.
	if team == my_team:
		_apply_progress(team, int(state["question_idx"]), next_quiz, finished)

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
	_apply_progress(pkt.team, pkt.question_idx, pkt.quiz_player_id, pkt.finished != 0)

func _apply_progress(_team: int, question_idx: int, quiz_player_id: int, finished: bool) -> void:
	my_question_idx = question_idx
	my_finished = finished
	# Meu papel a partir de agora é definido pela identidade do QUIZ atual.
	my_role = MinigameAssignPkt.ROLE.QUIZ if quiz_player_id == ClientPacketHandler.my_id else MinigameAssignPkt.ROLE.DOC
	_update_team_info_label()
	_refresh_hud_labels()
	_show_view_for_current_role()

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
