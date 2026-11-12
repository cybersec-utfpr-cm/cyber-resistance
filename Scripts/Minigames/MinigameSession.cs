using System;
using System.Collections.Generic;
using Godot;

/// Sessão de atividade cooperativa em dupla (Quiz e Palavras Cruzadas / Crossword) em C#.
/// Controla a sincronização dos 4 jogadores na sala, o sorteio das duplas (Dupla A e Dupla B),
/// a alternância entre os papéis DOC e QUIZ a cada rodada, a troca dinâmica de tarefas
/// comandada pelo Professor (MINIGAME_SELECT), o tabuleiro interativo da Cruzadinha,
/// o envio das respostas ao servidor central e a exibição da nota atribuída pelo Professor.
public partial class MinigameSession : Node
{
	public static bool IsActive { get; private set; }

	private const string QuizJsonPath = "res://prototype/data/minigame_quiz.json";
	private const int RequiredPlayers = 4;

	private Label _titleLabel;
	private Label _waitingLabel;
	private PanelContainer _documentPanel;
	private Label _docTitleLabel;
	private RichTextLabel _documentText;
	private PanelContainer _quizPanel;
	private Label _questionLabel;
	private LineEdit _answerLine;
	private Button _submitButton;
	private VBoxContainer _teamHud;
	private Label _timerLabel;
	private Label _progressLabel;
	private Label _teamInfoLabel;

	private Node _clientNet;
	private Node _clientPacketHandler;
	private Node _gamePacketHandler;
	private Node _playerHostPacketHandler;
	private Node _gameManager;

	private Godot.Collections.Dictionary _quizData = new();
	private readonly Dictionary<string, Godot.Collections.Dictionary> _activitiesCatalog = new();
	private string _currentActivityId = "defesa_redes";
	private int _totalQuestions;

	private bool _isCrosswordMode;
	private CrosswordGenerator.CrosswordLayout _crosswordLayout = new();
	private readonly List<string> _teamSubmittedAnswers = new();
	private VBoxContainer _crosswordQuizBox;
	private GridContainer _crosswordQuizGrid;
	private VBoxContainer _crosswordDocBox;
	private GridContainer _crosswordDocGrid;

	private int _myTeam = -1;
	private int _myRole = -1;
	private int _myPartnerId = -1;
	private readonly List<int> _myTeamMembers = new();
	private bool _assigned;

	private int _myQuestionIdx;
	private bool _myFinished;

	private class HostTeamState
	{
		public List<int> Members = new();
		public int QuizPlayerId;
		public int QuestionIdx;
		public List<string> Answers = new();
		public List<long> SubmissionMsecs = new();
		public long StartMsec;
		public bool Finished;
	}

	private readonly List<HostTeamState> _hostTeamStates = new();

	private long _startMsec;
	private float _myGrade = -1.0f;
	private string _myGradeComment = "";

	private Callable _onProgressCallable;
	private Callable _onGradeCallable;
	private Callable _onPlayerAnswerCallable;
	private Callable _onGradeLoopbackCallable;
	private Callable _onLobbyPlayersChangedCallable;
	private Callable _onHostPeerConnectedCallable;
	private Callable _onActivityChangedCallable;
	private Callable _onAssignRawCallable;

	public override void _Ready()
	{
		IsActive = true;

		_titleLabel = GetNodeOrNull<Label>("../Title");
		_waitingLabel = GetNode<Label>("../WaitingLabel");
		_documentPanel = GetNode<PanelContainer>("../DocumentPanel");
		_docTitleLabel = GetNode<Label>("../DocumentPanel/VBox/DocTitleLabel");
		_documentText = GetNode<RichTextLabel>("../DocumentPanel/VBox/DocumentText");
		_quizPanel = GetNode<PanelContainer>("../QuizPanel");
		_questionLabel = GetNode<Label>("../QuizPanel/VBox/QuestionLabel");
		_answerLine = GetNode<LineEdit>("../QuizPanel/VBox/AnswerLine");
		_submitButton = GetNode<Button>("../QuizPanel/VBox/SubmitButton");
		_teamHud = GetNode<VBoxContainer>("../TeamHUD");
		_timerLabel = GetNode<Label>("../TeamHUD/TimerLabel");
		_progressLabel = GetNode<Label>("../TeamHUD/ProgressLabel");
		_teamInfoLabel = GetNode<Label>("../TeamHUD/TeamInfoLabel");

		_clientNet = GetNodeOrNull("/root/ClientNet") ?? GetNodeOrNull("/root/ProtNetworkHandler");
		_clientPacketHandler = GetNodeOrNull("/root/ClientPacketHandler");
		_gamePacketHandler = GetNodeOrNull("/root/GamePacketHandler");
		_playerHostPacketHandler = GetNodeOrNull("/root/PlayerHostPacketHandler");
		_gameManager = GetNodeOrNull("/root/GameManager");

		if (!LoadQuiz())
		{
			GD.PushError($"Minigame: falha ao carregar {QuizJsonPath}");
			return;
		}

		_submitButton.Pressed += OnSubmitPressed;
		_answerLine.TextSubmitted += OnAnswerTextSubmitted;
		_answerLine.TextChanged += OnAnswerTextChanged;

		_onProgressCallable = Callable.From<byte[]>(OnProgressReceived);
		_onGradeCallable = Callable.From<byte[]>(OnGradeReceived);
		_onPlayerAnswerCallable = Callable.From<byte[]>(OnPlayerAnswerReceived);
		_onGradeLoopbackCallable = Callable.From<int, float, string>(OnGradeLoopback);
		_onLobbyPlayersChangedCallable = Callable.From<int>(OnLobbyPlayersChanged);
		_onHostPeerConnectedCallable = Callable.From<ENetPacketPeer>(OnHostPeerConnected);
		_onActivityChangedCallable = Callable.From<string>(OnActivityChanged);
		_onAssignRawCallable = Callable.From<byte[]>(OnAssignRawReceived);

		if (_playerHostPacketHandler != null)
		{
			_playerHostPacketHandler.Connect("host_minigame_progress_signal", _onProgressCallable);
			_playerHostPacketHandler.Connect("host_minigame_grade_signal", _onGradeCallable);
		}

		if (IsHost())
		{
			_playerHostPacketHandler?.Connect("player_minigame_answer_signal", _onPlayerAnswerCallable);
			_clientPacketHandler?.Connect("host_minigame_grade", _onGradeLoopbackCallable);
			_clientPacketHandler?.Connect("spawn_player_signal", _onLobbyPlayersChangedCallable);
			_gamePacketHandler?.Connect("peer_connected_to_host", _onHostPeerConnectedCallable);
			_clientPacketHandler?.Connect("activity_changed", _onActivityChangedCallable);
			TryHostStart();
		}
		else if (_clientPacketHandler != null)
		{
			_clientPacketHandler.Connect("minigame_assigned_raw", _onAssignRawCallable);
			byte[] pendingRaw = _clientPacketHandler.Get("pending_minigame_assign_raw").AsByteArray();
			if (pendingRaw != null && pendingRaw.Length > 0)
			{
				_clientPacketHandler.Set("pending_minigame_assign", new Variant());
				_clientPacketHandler.Set("pending_minigame_assign_raw", Array.Empty<byte>());
				MinigameAssignPkt pkt = MinigameAssignPkt.CreateFromData(pendingRaw);
				if (!string.IsNullOrEmpty(pkt.ActivityId))
				{
					SelectActivity(pkt.ActivityId);
				}
				ApplyAssignment(pkt.Team, pkt.Role, pkt.PartnerId, pkt.MemberIds);
			}
		}
	}

	public override void _ExitTree()
	{
		IsActive = false;
		if (_playerHostPacketHandler != null)
		{
			DisconnectIfConnected(_playerHostPacketHandler, "host_minigame_progress_signal", _onProgressCallable);
			DisconnectIfConnected(_playerHostPacketHandler, "host_minigame_grade_signal", _onGradeCallable);
			DisconnectIfConnected(_playerHostPacketHandler, "player_minigame_answer_signal", _onPlayerAnswerCallable);
		}
		if (_clientPacketHandler != null)
		{
			DisconnectIfConnected(_clientPacketHandler, "host_minigame_grade", _onGradeLoopbackCallable);
			DisconnectIfConnected(_clientPacketHandler, "spawn_player_signal", _onLobbyPlayersChangedCallable);
			DisconnectIfConnected(_clientPacketHandler, "activity_changed", _onActivityChangedCallable);
			DisconnectIfConnected(_clientPacketHandler, "minigame_assigned_raw", _onAssignRawCallable);
		}
		if (_gamePacketHandler != null)
		{
			DisconnectIfConnected(_gamePacketHandler, "peer_connected_to_host", _onHostPeerConnectedCallable);
		}
	}

	private static void DisconnectIfConnected(Node node, string signalName, Callable callable)
	{
		if (callable.Delegate != null && node.IsConnected(signalName, callable))
		{
			node.Disconnect(signalName, callable);
		}
	}

	public override void _Process(double delta)
	{
		if (!_assigned || _myFinished)
		{
			return;
		}
		long elapsed = ((long)Time.GetTicksMsec() - _startMsec) / 1000;
		_timerLabel.Text = $"Tempo: {elapsed}s";
	}

	private bool LoadQuiz()
	{
		FileAccess f = FileAccess.Open(QuizJsonPath, FileAccess.ModeFlags.Read);
		if (f == null)
		{
			return false;
		}
		string raw = f.GetAsText();
		f.Close();
		Variant parsed = Json.ParseString(raw);
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			return false;
		}
		Godot.Collections.Dictionary dict = parsed.AsGodotDictionary();
		_quizData = dict;
		_activitiesCatalog.Clear();

		if (dict.TryGetValue("activities", out Variant actsVar) && actsVar.VariantType == Variant.Type.Array)
		{
			foreach (Variant actItem in actsVar.AsGodotArray())
			{
				if (actItem.VariantType == Variant.Type.Dictionary)
				{
					Godot.Collections.Dictionary actDict = actItem.AsGodotDictionary();
					if (actDict.TryGetValue("id", out Variant idVar))
					{
						_activitiesCatalog[idVar.AsString()] = actDict;
					}
				}
			}
		}

		string initialId = _clientPacketHandler != null
			? _clientPacketHandler.Get("current_activity_id").AsString()
			: "";
		if (string.IsNullOrEmpty(initialId) && dict.TryGetValue("default_activity", out Variant defVar))
		{
			initialId = defVar.AsString();
		}
		if (!SelectActivity(initialId))
		{
			_totalQuestions = GetQuestionsArray(_quizData).Count;
		}
		return true;
	}

	private bool SelectActivity(string actId)
	{
		if (string.IsNullOrEmpty(actId) || !_activitiesCatalog.TryGetValue(actId, out Godot.Collections.Dictionary act))
		{
			return false;
		}
		_currentActivityId = actId;
		_clientPacketHandler?.Set("current_activity_id", actId);

		string targetScene = act.TryGetValue("scene", out Variant sceneVar) ? sceneVar.AsString() : "";
		if (!string.IsNullOrEmpty(targetScene) && GetTree().CurrentScene != null)
		{
			string currPath = GetTree().CurrentScene.SceneFilePath;
			if (!string.IsNullOrEmpty(currPath) && currPath != targetScene)
			{
				if (IsHost() && GetHostConnection() != null)
				{
					SceneForcePacket.Create(targetScene).Broadcast(GetHostConnection());
					_gameManager?.Call("goto_scene", targetScene);
				}
				return true;
			}
		}

		_quizData = act;
		Godot.Collections.Array questions = GetQuestionsArray(_quizData);
		_totalQuestions = questions.Count;
		_isCrosswordMode = act.TryGetValue("type", out Variant typeVar) && typeVar.AsString() == "crossword";
		_teamSubmittedAnswers.Clear();
		if (_isCrosswordMode)
		{
			var gen = new CrosswordGenerator();
			_crosswordLayout = gen.GenerateFromQuestions(questions);
		}
		else
		{
			_crosswordLayout = new CrosswordGenerator.CrosswordLayout();
		}

		if (_titleLabel != null)
		{
			string title = act.TryGetValue("title", out Variant titleVar) ? titleVar.AsString() : actId;
			_titleLabel.Text = $"Desafio cooperativo — {title}";
		}
		return true;
	}

	private void OnLobbyPlayersChanged(int _playerId)
	{
		if (IsHost())
		{
			TryHostStart();
		}
	}

	private void OnHostPeerConnected(ENetPacketPeer _peer)
	{
		if (IsHost())
		{
			TryHostStart();
		}
	}

	private void OnActivityChanged(string actId)
	{
		SelectActivity(actId);
		if (IsHost())
		{
			_assigned = false;
			_myFinished = false;
			_myQuestionIdx = 0;
			_myGrade = -1.0f;
			_myGradeComment = "";
			TryHostStart();
		}
	}

	private void TryHostStart()
	{
		if (_assigned)
		{
			return;
		}
		List<int> spawnedIds = GetSpawnedIds();
		int lobbyCount = spawnedIds.Count;
		int gamePeers = _gamePacketHandler != null
			? _gamePacketHandler.Call("get_connected_peer_count").AsInt32()
			: 0;
		if (lobbyCount < RequiredPlayers || gamePeers < RequiredPlayers - 1)
		{
			_documentPanel.Visible = false;
			_quizPanel.Visible = false;
			_teamHud.Visible = false;
			_waitingLabel.Visible = true;
			string actTitle = _quizData.TryGetValue("title", out Variant tVar)
				? tVar.AsString()
				: (_quizData.TryGetValue("document_title", out Variant dVar) ? dVar.AsString() : _currentActivityId);
			_waitingLabel.Text = $"Aguardando jogadores conectarem ({lobbyCount}/{RequiredPlayers})...\nTarefa selecionada: {actTitle}";
			return;
		}
		HostAssignAndDispatch();
	}

	private void HostAssignAndDispatch()
	{
		List<int> ids = GetSpawnedIds();
		if (ids.Count < RequiredPlayers)
		{
			return;
		}
		ids.Sort();

		long now = (long)Time.GetTicksMsec();
		_hostTeamStates.Clear();
		for (int t = 0; t < 2; t++)
		{
			_hostTeamStates.Add(new HostTeamState
			{
				Members = new List<int> { ids[t * 2], ids[t * 2 + 1] },
				QuizPlayerId = ids[t * 2 + 1],
				QuestionIdx = 0,
				StartMsec = now,
				Finished = false
			});
		}

		int myId = GetMyId();
		ENetConnection hostConn = GetHostConnection();
		for (int i = 0; i < RequiredPlayers; i++)
		{
			int playerId = ids[i];
			int team = i < 2 ? 0 : 1;
			int role = (i % 2 == 0) ? (int)MinigameAssignPkt.ROLE.DOC : (int)MinigameAssignPkt.ROLE.QUIZ;
			int partner = (i % 2 == 0) ? ids[i + 1] : ids[i - 1];
			var members = new List<int> { ids[i - (i % 2)], ids[i - (i % 2) + 1] };
			if (playerId == myId)
			{
				ApplyAssignment(team, role, partner, members);
			}
			else if (hostConn != null)
			{
				MinigameAssignPkt.Create(playerId, team, role, partner, members, _currentActivityId)
					.Broadcast(hostConn);
			}
		}
	}

	private void OnAssignRawReceived(byte[] data)
	{
		if (_clientPacketHandler != null)
		{
			_clientPacketHandler.Set("pending_minigame_assign", new Variant());
			_clientPacketHandler.Set("pending_minigame_assign_raw", Array.Empty<byte>());
		}
		MinigameAssignPkt pkt = MinigameAssignPkt.CreateFromData(data);
		if (!string.IsNullOrEmpty(pkt.ActivityId))
		{
			SelectActivity(pkt.ActivityId);
		}
		ApplyAssignment(pkt.Team, pkt.Role, pkt.PartnerId, pkt.MemberIds);
	}

	private void ApplyAssignment(int team, int role, int partner, IList<int> members)
	{
		_myTeam = team;
		_myRole = role;
		_myPartnerId = partner;
		_myTeamMembers.Clear();
		_myTeamMembers.AddRange(members);
		_myQuestionIdx = 0;
		_myFinished = false;
		_myGrade = -1.0f;
		_myGradeComment = "";
		_teamSubmittedAnswers.Clear();
		_assigned = true;
		_startMsec = (long)Time.GetTicksMsec();

		if (_clientPacketHandler != null)
		{
			_clientPacketHandler.Set("minigame_team", team);
			var gMembers = new Godot.Collections.Array<int>();
			foreach (int m in members)
			{
				gMembers.Add(m);
			}
			_clientPacketHandler.Set("minigame_team_members", gMembers);
		}

		_waitingLabel.Visible = false;
		_teamHud.Visible = true;
		UpdateTeamInfoLabel();
		RefreshHudLabels();
		ShowViewForCurrentRole();
	}

	private void ShowViewForCurrentRole()
	{
		if (_myFinished)
		{
			ShowFinishedState();
			return;
		}
		if (_myRole == (int)MinigameAssignPkt.ROLE.DOC)
		{
			SetupDocumentView();
		}
		else
		{
			SetupQuizView();
		}
	}

	private void SetupDocumentView()
	{
		_documentPanel.Visible = true;
		_quizPanel.Visible = false;
		_docTitleLabel.Text = _quizData.TryGetValue("document_title", out Variant dtVar)
			? dtVar.AsString()
			: "Documento";
		_documentText.Text = _quizData.TryGetValue("document_text", out Variant dtxtVar)
			? dtxtVar.AsString()
			: "";
		EnsureCrosswordUi();
		if (_isCrosswordMode)
		{
			_documentPanel.OffsetTop = -250.0f;
			_documentPanel.OffsetBottom = 250.0f;
			_documentText.CustomMinimumSize = new Vector2(0, 170);
			_crosswordDocBox.Visible = true;
			RebuildCrosswordBoards("");
		}
		else
		{
			_documentPanel.OffsetTop = -180.0f;
			_documentPanel.OffsetBottom = 180.0f;
			_documentText.CustomMinimumSize = new Vector2(0, 300);
			if (_crosswordDocBox != null)
			{
				_crosswordDocBox.Visible = false;
			}
		}
	}

	private void SetupQuizView()
	{
		_quizPanel.Visible = true;
		_documentPanel.Visible = false;
		EnsureCrosswordUi();
		if (_isCrosswordMode)
		{
			_quizPanel.OffsetTop = -250.0f;
			_quizPanel.OffsetBottom = 250.0f;
			_quizPanel.OffsetLeft = -420.0f;
			_quizPanel.OffsetRight = 420.0f;
			_crosswordQuizBox.Visible = true;
		}
		else
		{
			_quizPanel.OffsetTop = -180.0f;
			_quizPanel.OffsetBottom = 180.0f;
			_quizPanel.OffsetLeft = -360.0f;
			_quizPanel.OffsetRight = 360.0f;
			if (_crosswordQuizBox != null)
			{
				_crosswordQuizBox.Visible = false;
			}
		}
		ShowCurrentQuestion();
	}

	private void ShowCurrentQuestion()
	{
		Godot.Collections.Array questions = GetQuestionsArray(_quizData);
		if (questions.Count == 0)
		{
			_questionLabel.Text = "(sem perguntas no JSON)";
			return;
		}
		if (_myFinished || _myQuestionIdx >= questions.Count)
		{
			ShowFinishedState();
			return;
		}
		Godot.Collections.Dictionary q = questions[_myQuestionIdx].AsGodotDictionary();
		string prompt = q.TryGetValue("prompt", out Variant pVar) ? pVar.AsString() : "";

		if (_isCrosswordMode)
		{
			List<CrosswordGenerator.PlacedWord> words = _crosswordLayout.Words;
			string orient = "HORIZONTAL";
			string fallbackWord = q.TryGetValue("word", out Variant wVar)
				? wVar.AsString()
				: (q.TryGetValue("answer", out Variant aVar) ? aVar.AsString() : "");
			int wLen = fallbackWord.Length;
			if (_myQuestionIdx < words.Count)
			{
				CrosswordGenerator.PlacedWord wInfo = words[_myQuestionIdx];
				orient = wInfo.Vertical ? "VERTICAL" : "HORIZONTAL";
				wLen = wInfo.Word.Length;
			}
			_questionLabel.Text = $"Palavra #{_myQuestionIdx + 1} ({orient} — {wLen} letras): {prompt}";
			_answerLine.MaxLength = wLen;
			_answerLine.PlaceholderText = $"Digite a palavra de {wLen} letras...";
			RebuildCrosswordBoards("");
		}
		else
		{
			_questionLabel.Text = $"{_myQuestionIdx + 1}) {prompt}";
			_answerLine.MaxLength = 0;
			_answerLine.PlaceholderText = "Digite sua resposta";
		}

		_answerLine.Text = "";
		_answerLine.Editable = true;
		_submitButton.Disabled = false;
		_submitButton.Visible = true;
		_submitButton.Text = "Enviar";
		_answerLine.GrabFocus();
	}

	private void ShowFinishedState()
	{
		_quizPanel.Visible = false;
		_documentPanel.Visible = false;
		_waitingLabel.Visible = true;
		if (_myGrade >= 0.0f)
		{
			string txt = $"Nota da dupla: {_myGrade:F1}/10";
			if (!string.IsNullOrEmpty(_myGradeComment))
			{
				txt += "\n\nComentario do professor:\n" + _myGradeComment;
			}
			_waitingLabel.Text = txt;
		}
		else
		{
			_waitingLabel.Text = "Respostas enviadas. Aguarde o professor dar nota.";
		}
	}

	private void OnGradeReceived(byte[] data)
	{
		MinigameGradeResultPkt pkt = MinigameGradeResultPkt.CreateFromData(data);
		ApplyGrade(pkt.TeamIndex, pkt.Grade, pkt.Comment);
	}

	private void OnGradeLoopback(int teamIndex, float grade, string comment)
	{
		ApplyGrade(teamIndex, grade, comment);
	}

	private void ApplyGrade(int teamIndex, float grade, string comment)
	{
		if (teamIndex != _myTeam)
		{
			return;
		}
		_myGrade = grade;
		_myGradeComment = comment;
		if (_myFinished)
		{
			ShowFinishedState();
		}
	}

	private void OnAnswerTextSubmitted(string _text)
	{
		OnSubmitPressed();
	}

	private void OnAnswerTextChanged(string newText)
	{
		if (_isCrosswordMode && _myRole == (int)MinigameAssignPkt.ROLE.QUIZ)
		{
			RebuildCrosswordBoards(newText);
		}
	}

	private void OnSubmitPressed()
	{
		if (!_assigned || _myFinished)
		{
			return;
		}
		if (_myRole != (int)MinigameAssignPkt.ROLE.QUIZ)
		{
			return;
		}
		string raw = _answerLine.Text;
		if (string.IsNullOrWhiteSpace(raw))
		{
			return;
		}
		_submitButton.Disabled = true;
		_answerLine.Editable = false;
		if (IsHost())
		{
			HostProcessAnswer(_myTeam, _myQuestionIdx, raw);
		}
		else
		{
			ENetPacketPeer hostPeer = GetHostPeer();
			if (hostPeer != null)
			{
				MinigameAnswerPkt.Create(_myTeam, _myQuestionIdx, raw).Send(hostPeer);
			}
		}
	}

	private void OnPlayerAnswerReceived(byte[] data)
	{
		MinigameAnswerPkt pkt = MinigameAnswerPkt.CreateFromData(data);
		HostProcessAnswer(pkt.Team, pkt.QuestionIdx, pkt.Answer);
	}

	private void HostProcessAnswer(int team, int questionIdx, string rawAnswer)
	{
		if (team < 0 || team >= _hostTeamStates.Count)
		{
			return;
		}
		HostTeamState state = _hostTeamStates[team];
		if (state.Finished || questionIdx != state.QuestionIdx)
		{
			return;
		}
		Godot.Collections.Array questions = GetQuestionsArray(_quizData);
		if (questionIdx < 0 || questionIdx >= questions.Count)
		{
			return;
		}

		state.Answers.Add(rawAnswer);
		state.SubmissionMsecs.Add((long)Time.GetTicksMsec() - state.StartMsec);
		state.QuestionIdx += 1;

		int prevQuiz = state.QuizPlayerId;
		int nextQuiz = prevQuiz;
		foreach (int m in state.Members)
		{
			if (m != prevQuiz)
			{
				nextQuiz = m;
				break;
			}
		}
		state.QuizPlayerId = nextQuiz;

		bool finished = state.QuestionIdx >= questions.Count;
		state.Finished = finished;

		ENetConnection hostConn = GetHostConnection();
		if (hostConn != null)
		{
			MinigameProgressPkt.Create(team, state.QuestionIdx, nextQuiz, finished, rawAnswer)
				.Broadcast(hostConn);
		}
		if (team == _myTeam)
		{
			ApplyProgress(team, state.QuestionIdx, nextQuiz, finished, rawAnswer);
		}

		if (finished && AllTeamsFinished())
		{
			HostSubmitToCentral();
		}
	}

	private bool AllTeamsFinished()
	{
		foreach (HostTeamState state in _hostTeamStates)
		{
			if (!state.Finished)
			{
				return false;
			}
		}
		return true;
	}

	private void HostSubmitToCentral()
	{
		ENetPacketPeer serverPeer = GetServerPeer();
		if (serverPeer == null)
		{
			GD.PushError("Minigame: host sem conexão com servidor central; submissão perdida.");
			return;
		}
		long now = (long)Time.GetTicksMsec();
		var teamsPayload = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		foreach (HostTeamState state in _hostTeamStates)
		{
			var membersArr = new Godot.Collections.Array();
			foreach (int m in state.Members)
			{
				membersArr.Add(m);
			}
			var answersArr = new Godot.Collections.Array();
			foreach (string a in state.Answers)
			{
				answersArr.Add(a);
			}
			var msecsArr = new Godot.Collections.Array();
			foreach (long ms in state.SubmissionMsecs)
			{
				msecsArr.Add(ms);
			}
			teamsPayload.Add(new Godot.Collections.Dictionary
			{
				{ "members", membersArr },
				{ "total_msec", now - state.StartMsec },
				{ "answers", answersArr },
				{ "submission_msecs", msecsArr }
			});
		}
		int roomId = _clientPacketHandler != null
			? _clientPacketHandler.Get("current_room_id").AsInt32()
			: 0;
		MinigameSubmissionPkt.Create(roomId, teamsPayload).Send(serverPeer);
	}

	private void OnProgressReceived(byte[] data)
	{
		MinigameProgressPkt pkt = MinigameProgressPkt.CreateFromData(data);
		if (pkt.Team != _myTeam)
		{
			return;
		}
		ApplyProgress(pkt.Team, pkt.QuestionIdx, pkt.QuizPlayerId, pkt.Finished != 0, pkt.LastAnswer);
	}

	private void ApplyProgress(int _team, int questionIdx, int quizPlayerId, bool finished, string lastAnswer = "")
	{
		if (!string.IsNullOrEmpty(lastAnswer))
		{
			_teamSubmittedAnswers.Add(CrosswordGenerator.NormalizeWord(lastAnswer));
		}
		_myQuestionIdx = questionIdx;
		_myFinished = finished;
		_myRole = (quizPlayerId == GetMyId())
			? (int)MinigameAssignPkt.ROLE.QUIZ
			: (int)MinigameAssignPkt.ROLE.DOC;
		UpdateTeamInfoLabel();
		RefreshHudLabels();
		ShowViewForCurrentRole();
	}

	private void UpdateTeamInfoLabel()
	{
		if (_myTeam < 0)
		{
			return;
		}
		string myRoleText = _myRole == (int)MinigameAssignPkt.ROLE.QUIZ ? "QUIZ" : "DOC";
		string partnerRoleText = _myRole == (int)MinigameAssignPkt.ROLE.QUIZ ? "DOC" : "QUIZ";
		string teamLetter = _myTeam == 0 ? "A" : "B";
		_teamInfoLabel.Text = $"Dupla {teamLetter} — você: {myRoleText} | parceiro: player_{_myPartnerId} ({partnerRoleText})";
	}

	private void RefreshHudLabels()
	{
		if (_myFinished)
		{
			_progressLabel.Text = $"Concluído ({_totalQuestions}/{_totalQuestions})";
		}
		else
		{
			_progressLabel.Text = $"Pergunta {_myQuestionIdx + 1} de {_totalQuestions}";
		}
	}

	private void EnsureCrosswordUi()
	{
		if (_crosswordQuizBox == null)
		{
			VBoxContainer qVBox = GetNode<VBoxContainer>("../QuizPanel/VBox");
			_crosswordQuizBox = new VBoxContainer { Name = "CrosswordQuizBox" };
			var hintLbl = new Label
			{
				Text = "Tabuleiro da Cruzadinha (células azuis = palavra da vez; verdes = já preenchidas)",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			hintLbl.AddThemeFontSizeOverride("font_size", 12);
			_crosswordQuizBox.AddChild(hintLbl);

			var center = new CenterContainer();
			_crosswordQuizGrid = new GridContainer();
			_crosswordQuizGrid.AddThemeConstantOverride("h_separation", 2);
			_crosswordQuizGrid.AddThemeConstantOverride("v_separation", 2);
			center.AddChild(_crosswordQuizGrid);
			_crosswordQuizBox.AddChild(center);

			qVBox.AddChild(_crosswordQuizBox);
			qVBox.MoveChild(_crosswordQuizBox, 0);
		}

		if (_crosswordDocBox == null)
		{
			VBoxContainer dVBox = GetNode<VBoxContainer>("../DocumentPanel/VBox");
			_crosswordDocBox = new VBoxContainer { Name = "CrosswordDocBox" };
			var docHint = new Label
			{
				Text = "Progresso da Dupla na Cruzadinha (acompanhe os cruzamentos do seu parceiro)",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			docHint.AddThemeFontSizeOverride("font_size", 12);
			_crosswordDocBox.AddChild(docHint);

			var centerDoc = new CenterContainer();
			_crosswordDocGrid = new GridContainer();
			_crosswordDocGrid.AddThemeConstantOverride("h_separation", 2);
			_crosswordDocGrid.AddThemeConstantOverride("v_separation", 2);
			centerDoc.AddChild(_crosswordDocGrid);
			_crosswordDocBox.AddChild(centerDoc);

			dVBox.AddChild(_crosswordDocBox);
		}
	}

	private class CellRenderInfo
	{
		public bool Active;
		public bool Filled;
		public string Letter = "";
		public int Num;
	}

	private void RebuildCrosswordBoards(string previewTyping = "")
	{
		if (!_isCrosswordMode || _crosswordLayout.Words.Count == 0)
		{
			return;
		}
		EnsureCrosswordUi();
		GridContainer targetGrid = (_myRole == (int)MinigameAssignPkt.ROLE.QUIZ)
			? _crosswordQuizGrid
			: _crosswordDocGrid;
		if (targetGrid == null)
		{
			return;
		}

		foreach (Node child in targetGrid.GetChildren())
		{
			child.QueueFree();
		}

		int rowsCnt = Math.Max(1, _crosswordLayout.Rows);
		int colsCnt = Math.Max(1, _crosswordLayout.Cols);
		List<CrosswordGenerator.PlacedWord> words = _crosswordLayout.Words;
		targetGrid.Columns = colsCnt;

		var cellMap = new Dictionary<string, CellRenderInfo>();
		for (int wIdx = 0; wIdx < words.Count; wIdx++)
		{
			CrosswordGenerator.PlacedWord wInfo = words[wIdx];
			int wr = wInfo.Row;
			int wc = wInfo.Col;
			bool wvert = wInfo.Vertical;
			int wlen = wInfo.Word.Length;
			int wnum = wInfo.Number;
			string submittedStr = wIdx < _teamSubmittedAnswers.Count ? _teamSubmittedAnswers[wIdx] : "";

			for (int i = 0; i < wlen; i++)
			{
				int r = wr + (wvert ? i : 0);
				int c = wc + (wvert ? 0 : i);
				string key = $"{r}_{c}";
				if (!cellMap.TryGetValue(key, out CellRenderInfo entry))
				{
					entry = new CellRenderInfo();
					cellMap[key] = entry;
				}
				if (i == 0 && entry.Num == 0)
				{
					entry.Num = wnum;
				}
				if (!string.IsNullOrEmpty(submittedStr) && i < submittedStr.Length)
				{
					entry.Letter = submittedStr.Substring(i, 1);
					entry.Filled = true;
				}
				if (wIdx == _myQuestionIdx && !_myFinished)
				{
					entry.Active = true;
				}
			}
		}

		string cleanPreview = CrosswordGenerator.NormalizeWord(previewTyping);
		if (!string.IsNullOrEmpty(cleanPreview) && _myQuestionIdx < words.Count && !_myFinished)
		{
			CrosswordGenerator.PlacedWord curW = words[_myQuestionIdx];
			int limit = Math.Min(cleanPreview.Length, curW.Word.Length);
			for (int i = 0; i < limit; i++)
			{
				int r = curW.Row + (curW.Vertical ? i : 0);
				int c = curW.Col + (curW.Vertical ? 0 : i);
				string key = $"{r}_{c}";
				if (cellMap.TryGetValue(key, out CellRenderInfo entry))
				{
					entry.Letter = cleanPreview.Substring(i, 1);
				}
			}
		}

		int cellPx = Math.Clamp((int)(260.0f / rowsCnt), 20, 28);
		for (int r = 0; r < rowsCnt; r++)
		{
			for (int c = 0; c < colsCnt; c++)
			{
				string key = $"{r}_{c}";
				var cell = new Panel
				{
					CustomMinimumSize = new Vector2(cellPx, cellPx)
				};
				var style = new StyleBoxFlat();
				if (!cellMap.TryGetValue(key, out CellRenderInfo info))
				{
					style.BgColor = new Color(0.08f, 0.09f, 0.14f, 0.25f);
					cell.AddThemeStyleboxOverride("panel", style);
					targetGrid.AddChild(cell);
					continue;
				}

				style.SetBorderWidthAll(1);
				if (info.Active)
				{
					style.BgColor = new Color(0.16f, 0.38f, 0.54f, 1.0f);
					style.BorderColor = new Color(0.45f, 0.85f, 1.0f, 1.0f);
				}
				else if (info.Filled)
				{
					style.BgColor = new Color(0.15f, 0.33f, 0.24f, 1.0f);
					style.BorderColor = new Color(0.35f, 0.75f, 0.5f, 1.0f);
				}
				else
				{
					style.BgColor = new Color(0.2f, 0.22f, 0.32f, 1.0f);
					style.BorderColor = new Color(0.4f, 0.44f, 0.58f, 1.0f);
				}
				cell.AddThemeStyleboxOverride("panel", style);

				if (info.Num > 0)
				{
					var numLbl = new Label
					{
						Text = info.Num.ToString(),
						Position = new Vector2(2, 0)
					};
					numLbl.AddThemeFontSizeOverride("font_size", 8);
					numLbl.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.4f, 1.0f));
					cell.AddChild(numLbl);
				}

				var chLbl = new Label
				{
					Text = info.Letter,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				chLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
				chLbl.AddThemeFontSizeOverride("font_size", 12);
				cell.AddChild(chLbl);

				targetGrid.AddChild(cell);
			}
		}
	}

	private bool IsHost() =>
		_gamePacketHandler != null && _gamePacketHandler.Get("is_host").AsBool();

	private int GetMyId() =>
		_clientPacketHandler != null ? _clientPacketHandler.Get("my_id").AsInt32() : -1;

	private List<int> GetSpawnedIds()
	{
		var result = new List<int>();
		if (_clientPacketHandler == null)
		{
			return result;
		}
		Godot.Collections.Array arr = _clientPacketHandler.Get("spawned_ids").AsGodotArray();
		foreach (Variant v in arr)
		{
			result.Add(v.AsInt32());
		}
		return result;
	}

	private ENetConnection GetHostConnection() =>
		_gamePacketHandler?.Get("host_connection").AsGodotObject() as ENetConnection;

	private ENetPacketPeer GetHostPeer() =>
		_gamePacketHandler?.Get("host_peer").AsGodotObject() as ENetPacketPeer;

	private ENetPacketPeer GetServerPeer() =>
		_clientNet?.Get("server_peer").AsGodotObject() as ENetPacketPeer;

	private static Godot.Collections.Array GetQuestionsArray(Godot.Collections.Dictionary dict)
	{
		if (dict != null && dict.TryGetValue("questions", out Variant qVar) && qVar.VariantType == Variant.Type.Array)
		{
			return qVar.AsGodotArray();
		}
		return new Godot.Collections.Array();
	}
}
