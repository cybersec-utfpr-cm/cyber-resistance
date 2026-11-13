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
	private bool _isWordSearchMode;
	private CrosswordGenerator.CrosswordLayout _crosswordLayout = new();
	private WordSearchGenerator.WordSearchBoard _wordSearchBoard = new();
	private readonly List<string> _teamSubmittedAnswers = new();
	private readonly List<Vector2I> _wsClickedPath = new();
	private VBoxContainer _crosswordQuizBox;
	private Label _crosswordQuizHint;
	private GridContainer _crosswordQuizGrid;
	private HBoxContainer _wsMainSplit;
	private GridContainer _wsGrid;
	private HBoxContainer _wsLetterSlotsBox;
	private Button _wsClearBtn;
	private Button _wsConfirmBtn;
	private RichTextLabel _wsChecklistLabel;
	private VBoxContainer _crosswordDocBox;
	private Label _crosswordDocHint;
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
		string actType = act.TryGetValue("type", out Variant typeVar) ? typeVar.AsString() : "quiz";
		_isCrosswordMode = actType == "crossword";
		_isWordSearchMode = actType == "wordsearch";
		_teamSubmittedAnswers.Clear();
		_wsClickedPath.Clear();
		if (_isCrosswordMode)
		{
			var gen = new CrosswordGenerator();
			_crosswordLayout = gen.GenerateFromQuestions(questions);
			_wordSearchBoard = new WordSearchGenerator.WordSearchBoard();
		}
		else if (_isWordSearchMode)
		{
			var wsGen = new WordSearchGenerator();
			_wordSearchBoard = wsGen.GenerateFromQuestions(questions);
			_crosswordLayout = new CrosswordGenerator.CrosswordLayout();
		}
		else
		{
			_crosswordLayout = new CrosswordGenerator.CrosswordLayout();
			_wordSearchBoard = new WordSearchGenerator.WordSearchBoard();
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
			if (_isWordSearchMode && _assigned)
			{
				HostSyncIndividualPlayers();
			}
			else
			{
				TryHostStart();
			}
		}
	}

	private void OnHostPeerConnected(ENetPacketPeer _peer)
	{
		if (IsHost())
		{
			if (_isWordSearchMode && _assigned)
			{
				HostSyncIndividualPlayers();
			}
			else
			{
				TryHostStart();
			}
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

		// No modo individual (Caça-Palavras), cada aluno joga no seu próprio tabuleiro;
		// inicia assim que o host e os colegas já presentes estiverem conectados na camada 2.
		int minRequired = _isWordSearchMode ? Math.Max(1, lobbyCount) : RequiredPlayers;
		if (lobbyCount < minRequired || gamePeers < lobbyCount - 1)
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

	private void HostSyncIndividualPlayers()
	{
		List<int> ids = GetSpawnedIds();
		int gamePeers = _gamePacketHandler != null
			? _gamePacketHandler.Call("get_connected_peer_count").AsInt32()
			: 0;
		if (gamePeers < ids.Count - 1)
		{
			return;
		}
		ids.Sort();
		long now = (long)Time.GetTicksMsec();
		ENetConnection hostConn = GetHostConnection();
		int myId = GetMyId();

		for (int i = 0; i < ids.Count; i++)
		{
			int playerId = ids[i];
			bool exists = false;
			for (int t = 0; t < _hostTeamStates.Count; t++)
			{
				if (_hostTeamStates[t].QuizPlayerId == playerId)
				{
					exists = true;
					break;
				}
			}
			if (!exists)
			{
				int teamIdx = _hostTeamStates.Count;
				var members = new List<int> { playerId };
				_hostTeamStates.Add(new HostTeamState
				{
					Members = members,
					QuizPlayerId = playerId,
					QuestionIdx = 0,
					StartMsec = now,
					Finished = false
				});
				if (playerId != myId && hostConn != null)
				{
					MinigameAssignPkt.Create(playerId, teamIdx, (int)MinigameAssignPkt.ROLE.QUIZ, playerId, members, _currentActivityId)
						.Broadcast(hostConn);
				}
			}
		}
	}

	private void HostAssignAndDispatch()
	{
		List<int> ids = GetSpawnedIds();
		if (!_isWordSearchMode && ids.Count < RequiredPlayers)
		{
			return;
		}
		if (ids.Count == 0)
		{
			return;
		}
		ids.Sort();

		long now = (long)Time.GetTicksMsec();
		_hostTeamStates.Clear();
		int myId = GetMyId();
		ENetConnection hostConn = GetHostConnection();

		if (_isWordSearchMode)
		{
			// Modo Individual: cada jogador recebe seu próprio índice (team = i) e papel QUIZ.
			for (int i = 0; i < ids.Count; i++)
			{
				int playerId = ids[i];
				var members = new List<int> { playerId };
				_hostTeamStates.Add(new HostTeamState
				{
					Members = members,
					QuizPlayerId = playerId,
					QuestionIdx = 0,
					StartMsec = now,
					Finished = false
				});
				if (playerId == myId)
				{
					ApplyAssignment(i, (int)MinigameAssignPkt.ROLE.QUIZ, playerId, members);
				}
				else if (hostConn != null)
				{
					MinigameAssignPkt.Create(playerId, i, (int)MinigameAssignPkt.ROLE.QUIZ, playerId, members, _currentActivityId)
						.Broadcast(hostConn);
				}
			}
			return;
		}

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
		else if (_isWordSearchMode)
		{
			_documentPanel.OffsetTop = -255.0f;
			_documentPanel.OffsetBottom = 255.0f;
			_documentText.CustomMinimumSize = new Vector2(0, 150);
			_crosswordDocBox.Visible = true;
			RebuildWordSearchBoards("");
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
		if (_isWordSearchMode)
		{
			_quizPanel.OffsetTop = -270.0f;
			_quizPanel.OffsetBottom = 270.0f;
			_quizPanel.OffsetLeft = -520.0f;
			_quizPanel.OffsetRight = 520.0f;
			_crosswordQuizBox.Visible = true;
			if (_crosswordQuizGrid != null && _crosswordQuizGrid.GetParent() is Control cwCenter)
			{
				cwCenter.Visible = false;
			}
			if (_wsMainSplit != null)
			{
				_wsMainSplit.Visible = true;
			}
		}
		else if (_isCrosswordMode)
		{
			_quizPanel.OffsetTop = -255.0f;
			_quizPanel.OffsetBottom = 255.0f;
			_quizPanel.OffsetLeft = -420.0f;
			_quizPanel.OffsetRight = 420.0f;
			_crosswordQuizBox.Visible = true;
			if (_crosswordQuizGrid != null && _crosswordQuizGrid.GetParent() is Control cwCenter)
			{
				cwCenter.Visible = true;
			}
			if (_wsMainSplit != null)
			{
				_wsMainSplit.Visible = false;
			}
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

		_wsClickedPath.Clear();

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
			_answerLine.Visible = true;
			_submitButton.Visible = true;
			RebuildCrosswordBoards("");
		}
		else if (_isWordSearchMode)
		{
			List<WordSearchGenerator.PlacedWord> words = _wordSearchBoard.Words;
			string fallbackWord = q.TryGetValue("word", out Variant wVar)
				? wVar.AsString()
				: (q.TryGetValue("answer", out Variant aVar) ? aVar.AsString() : "");
			int wLen = fallbackWord.Length;
			if (_myQuestionIdx < words.Count)
			{
				wLen = words[_myQuestionIdx].Word.Length;
			}
			_questionLabel.Text = $"Missão Atual — Palavra #{_myQuestionIdx + 1} ({wLen} letras): {prompt}";
			_answerLine.MaxLength = wLen;
			// No Caça-Palavras Individual Especial, o aluno forma a palavra clicando nos quadrados!
			_answerLine.Visible = false;
			_submitButton.Visible = false;
			_answerLine.Text = "";
			RebuildWordSearchBoards("");
			return;
		}
		else
		{
			_questionLabel.Text = $"{_myQuestionIdx + 1}) {prompt}";
			_answerLine.MaxLength = 0;
			_answerLine.PlaceholderText = "Digite sua resposta";
			_answerLine.Visible = true;
			_submitButton.Visible = true;
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
			string labelPrefix = _isWordSearchMode ? "Nota individual" : "Nota da dupla";
			string txt = $"{labelPrefix}: {_myGrade:F1}/10";
			if (!string.IsNullOrEmpty(_myGradeComment))
			{
				txt += "\n\nComentario do professor:\n" + _myGradeComment;
			}
			_waitingLabel.Text = txt;
		}
		else
		{
			_waitingLabel.Text = _isWordSearchMode
				? "Caça-Palavras concluído! Aguarde o professor enviar sua nota."
				: "Respostas enviadas. Aguarde o professor dar nota.";
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
		if (_myRole != (int)MinigameAssignPkt.ROLE.QUIZ)
		{
			return;
		}
		if (_isCrosswordMode)
		{
			RebuildCrosswordBoards(newText);
		}
		else if (_isWordSearchMode)
		{
			RebuildWordSearchBoards(newText);
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
		if (_isWordSearchMode)
		{
			_teamInfoLabel.Text = $"Modo Individual — Aluno: player_{GetMyId()} | Encontradas: {_myQuestionIdx}/{_totalQuestions}";
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
		else if (_isWordSearchMode)
		{
			_progressLabel.Text = $"Palavra {_myQuestionIdx + 1} de {_totalQuestions}";
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
			_crosswordQuizBox.AddThemeConstantOverride("separation", 6);
			_crosswordQuizHint = new Label
			{
				Text = "Tabuleiro da Cruzadinha (células azuis = palavra da vez; verdes = já preenchidas)",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_crosswordQuizHint.AddThemeFontSizeOverride("font_size", 12);
			_crosswordQuizBox.AddChild(_crosswordQuizHint);

			var center = new CenterContainer { Name = "CrosswordCenter" };
			_crosswordQuizGrid = new GridContainer();
			_crosswordQuizGrid.AddThemeConstantOverride("h_separation", 2);
			_crosswordQuizGrid.AddThemeConstantOverride("v_separation", 2);
			center.AddChild(_crosswordQuizGrid);
			_crosswordQuizBox.AddChild(center);

			// UI especial do Caça-Palavras Individual (Grade clicável à esquerda + Checklist à direita)
			_wsMainSplit = new HBoxContainer
			{
				Name = "WordSearchMainSplit",
				Alignment = BoxContainer.AlignmentMode.Center,
				Visible = false
			};
			_wsMainSplit.AddThemeConstantOverride("separation", 18);

			var leftVBox = new VBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center
			};
			leftVBox.AddThemeConstantOverride("separation", 8);

			var wsGridCenter = new CenterContainer();
			_wsGrid = new GridContainer();
			_wsGrid.AddThemeConstantOverride("h_separation", 3);
			_wsGrid.AddThemeConstantOverride("v_separation", 3);
			wsGridCenter.AddChild(_wsGrid);
			leftVBox.AddChild(wsGridCenter);

			_wsLetterSlotsBox = new HBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center
			};
			_wsLetterSlotsBox.AddThemeConstantOverride("separation", 4);
			leftVBox.AddChild(_wsLetterSlotsBox);

			var btnRow = new HBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center
			};
			btnRow.AddThemeConstantOverride("separation", 10);

			_wsClearBtn = new Button
			{
				Text = "⌫ Limpar Seleção",
				CustomMinimumSize = new Vector2(145, 32)
			};
			_wsClearBtn.Pressed += OnWordSearchClearPressed;
			btnRow.AddChild(_wsClearBtn);

			_wsConfirmBtn = new Button
			{
				Text = "✔ Confirmar Palavra",
				CustomMinimumSize = new Vector2(165, 32)
			};
			_wsConfirmBtn.Pressed += OnWordSearchConfirmPressed;
			btnRow.AddChild(_wsConfirmBtn);

			leftVBox.AddChild(btnRow);
			_wsMainSplit.AddChild(leftVBox);

			var rightPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(360, 340)
			};
			var rightStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.09f, 0.11f, 0.17f, 0.95f),
				BorderColor = new Color(0.28f, 0.36f, 0.52f, 1.0f)
			};
			rightStyle.SetBorderWidthAll(1);
			rightStyle.SetContentMarginAll(10);
			rightPanel.AddThemeStyleboxOverride("panel", rightStyle);

			var rightVBox = new VBoxContainer();
			rightVBox.AddThemeConstantOverride("separation", 6);
			var rightTitle = new Label
			{
				Text = "Painel de Missões — Termos Cyber",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			rightTitle.AddThemeFontSizeOverride("font_size", 13);
			rightTitle.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1.0f, 1.0f));
			rightVBox.AddChild(rightTitle);

			_wsChecklistLabel = new RichTextLabel
			{
				BbcodeEnabled = true,
				FitContent = false,
				ScrollActive = true,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(335, 290)
			};
			_wsChecklistLabel.AddThemeFontSizeOverride("normal_font_size", 12);
			_wsChecklistLabel.AddThemeFontSizeOverride("bold_font_size", 12);
			rightVBox.AddChild(_wsChecklistLabel);
			rightPanel.AddChild(rightVBox);

			_wsMainSplit.AddChild(rightPanel);
			_crosswordQuizBox.AddChild(_wsMainSplit);

			qVBox.AddChild(_crosswordQuizBox);
			qVBox.MoveChild(_crosswordQuizBox, 0);
		}

		if (_crosswordDocBox == null)
		{
			VBoxContainer dVBox = GetNode<VBoxContainer>("../DocumentPanel/VBox");
			_crosswordDocBox = new VBoxContainer { Name = "CrosswordDocBox" };
			_crosswordDocHint = new Label
			{
				Text = "Progresso da Dupla na Cruzadinha (acompanhe os cruzamentos do seu parceiro)",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_crosswordDocHint.AddThemeFontSizeOverride("font_size", 12);
			_crosswordDocBox.AddChild(_crosswordDocHint);

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
		if (_crosswordQuizHint != null)
		{
			_crosswordQuizHint.Text = "Tabuleiro da Cruzadinha (células azuis = palavra da vez; verdes = já preenchidas)";
		}
		if (_crosswordDocHint != null)
		{
			_crosswordDocHint.Text = "Progresso da Dupla na Cruzadinha (acompanhe os cruzamentos do seu parceiro)";
		}
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

	private string GetFormedWordSearchString()
	{
		if (_wordSearchBoard == null || _wsClickedPath.Count == 0)
		{
			return "";
		}
		var sb = new System.Text.StringBuilder(_wsClickedPath.Count);
		foreach (Vector2I pos in _wsClickedPath)
		{
			if (pos.X >= 0 && pos.X < _wordSearchBoard.Rows && pos.Y >= 0 && pos.Y < _wordSearchBoard.Cols)
			{
				sb.Append(_wordSearchBoard.Grid[pos.X, pos.Y]);
			}
		}
		return sb.ToString();
	}

	private void OnWordSearchClearPressed()
	{
		_wsClickedPath.Clear();
		_answerLine.Text = "";
		RebuildWordSearchBoards("");
	}

	private void OnWordSearchConfirmPressed()
	{
		string formed = GetFormedWordSearchString();
		if (string.IsNullOrWhiteSpace(formed))
		{
			return;
		}
		_answerLine.Text = formed;
		OnSubmitPressed();
	}

	private void RebuildWordSearchBoards(string _previewTyping = "")
	{
		if (!_isWordSearchMode || _wordSearchBoard.Words.Count == 0)
		{
			return;
		}
		EnsureCrosswordUi();
		List<WordSearchGenerator.PlacedWord> words = _wordSearchBoard.Words;
		Godot.Collections.Array questions = GetQuestionsArray(_quizData);

		int targetLen = 0;
		if (_myQuestionIdx < words.Count && !_myFinished)
		{
			targetLen = words[_myQuestionIdx].Word.Length;
		}

		string formedWord = GetFormedWordSearchString();
		_answerLine.Text = formedWord;

		if (_crosswordQuizHint != null)
		{
			if (_wsClickedPath.Count == 0)
			{
				_crosswordQuizHint.Text = $"Modo Individual: clique nos quadrados para formar a Palavra #{_myQuestionIdx + 1} ({targetLen} letras)!";
			}
			else if (_wsClickedPath.Count < targetLen)
			{
				_crosswordQuizHint.Text = $"Formando: \"{formedWord}\" ({_wsClickedPath.Count}/{targetLen} letras) — continue clicando nos quadrados em linha reta!";
			}
			else
			{
				_crosswordQuizHint.Text = $"Palavra formada: \"{formedWord}\" ({_wsClickedPath.Count} letras) — clique em [✔ Confirmar Palavra]!";
			}
		}

		if (_wsGrid == null)
		{
			return;
		}

		foreach (Node child in _wsGrid.GetChildren())
		{
			child.QueueFree();
		}

		int rowsCnt = Math.Max(1, _wordSearchBoard.Rows);
		int colsCnt = Math.Max(1, _wordSearchBoard.Cols);
		_wsGrid.Columns = colsCnt;

		var foundCells = new HashSet<string>();
		var startNums = new Dictionary<string, int>();
		for (int wIdx = 0; wIdx < Math.Min(_teamSubmittedAnswers.Count, words.Count); wIdx++)
		{
			WordSearchGenerator.PlacedWord wInfo = words[wIdx];
			string startKey = $"{wInfo.StartRow}_{wInfo.StartCol}";
			if (!startNums.ContainsKey(startKey))
			{
				startNums[startKey] = wInfo.Number;
			}
			for (int i = 0; i < wInfo.Word.Length; i++)
			{
				int r = wInfo.StartRow + wInfo.Dr * i;
				int c = wInfo.StartCol + wInfo.Dc * i;
				foundCells.Add($"{r}_{c}");
			}
		}

		var clickedIndexMap = new Dictionary<string, int>();
		for (int i = 0; i < _wsClickedPath.Count; i++)
		{
			Vector2I p = _wsClickedPath[i];
			clickedIndexMap[$"{p.X}_{p.Y}"] = i + 1;
		}

		int cellPx = 27;
		for (int r = 0; r < rowsCnt; r++)
		{
			for (int c = 0; c < colsCnt; c++)
			{
				string key = $"{r}_{c}";
				var cell = new Panel
				{
					CustomMinimumSize = new Vector2(cellPx, cellPx),
					MouseFilter = Control.MouseFilterEnum.Stop,
					MouseDefaultCursorShape = Control.CursorShape.PointingHand
				};

				var style = new StyleBoxFlat();
				style.SetBorderWidthAll(1);
				style.SetCornerRadiusAll(3);

				bool isSelected = clickedIndexMap.TryGetValue(key, out int clickOrder);
				bool isLastClicked = isSelected && clickOrder == _wsClickedPath.Count;
				bool isFound = foundCells.Contains(key);

				if (isLastClicked)
				{
					style.BgColor = new Color(0.22f, 0.56f, 0.78f, 1.0f);
					style.BorderColor = new Color(1.0f, 0.92f, 0.45f, 1.0f);
					style.SetBorderWidthAll(2);
				}
				else if (isSelected)
				{
					style.BgColor = new Color(0.16f, 0.44f, 0.65f, 1.0f);
					style.BorderColor = new Color(0.48f, 0.9f, 1.0f, 1.0f);
				}
				else if (isFound)
				{
					style.BgColor = new Color(0.14f, 0.36f, 0.24f, 1.0f);
					style.BorderColor = new Color(0.35f, 0.82f, 0.52f, 1.0f);
				}
				else
				{
					style.BgColor = new Color(0.16f, 0.19f, 0.27f, 1.0f);
					style.BorderColor = new Color(0.3f, 0.35f, 0.48f, 1.0f);
				}
				cell.AddThemeStyleboxOverride("panel", style);

				if (isSelected)
				{
					var orderLbl = new Label
					{
						Text = clickOrder.ToString(),
						Position = new Vector2(2, -1)
					};
					orderLbl.AddThemeFontSizeOverride("font_size", 8);
					orderLbl.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.4f, 1.0f));
					cell.AddChild(orderLbl);
				}
				else if (startNums.TryGetValue(key, out int sn) && sn > 0)
				{
					var numLbl = new Label
					{
						Text = sn.ToString(),
						Position = new Vector2(2, -1)
					};
					numLbl.AddThemeFontSizeOverride("font_size", 8);
					numLbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.68f, 1.0f));
					cell.AddChild(numLbl);
				}

				var chLbl = new Label
				{
					Text = _wordSearchBoard.Grid[r, c],
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				chLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
				chLbl.AddThemeFontSizeOverride("font_size", 13);
				if (isSelected)
				{
					chLbl.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
				}
				cell.AddChild(chLbl);

				if (!_myFinished)
				{
					int capturedR = r;
					int capturedC = c;
					cell.GuiInput += (@event) => OnWordSearchCellGuiInput(@event, capturedR, capturedC);
				}

				_wsGrid.AddChild(cell);
			}
		}

		// Atualiza os slots visuais de letras formadas abaixo da grade
		if (_wsLetterSlotsBox != null)
		{
			foreach (Node child in _wsLetterSlotsBox.GetChildren())
			{
				child.QueueFree();
			}
			int slotsCount = Math.Max(targetLen, _wsClickedPath.Count);
			for (int i = 0; i < slotsCount; i++)
			{
				bool hasLetter = i < formedWord.Length;
				var slotPanel = new Panel
				{
					CustomMinimumSize = new Vector2(28, 30)
				};
				var slotStyle = new StyleBoxFlat();
				slotStyle.SetBorderWidthAll(1);
				slotStyle.SetCornerRadiusAll(4);
				if (hasLetter)
				{
					slotStyle.BgColor = new Color(0.18f, 0.45f, 0.66f, 1.0f);
					slotStyle.BorderColor = new Color(0.55f, 0.92f, 1.0f, 1.0f);
				}
				else
				{
					slotStyle.BgColor = new Color(0.11f, 0.13f, 0.19f, 1.0f);
					slotStyle.BorderColor = new Color(0.32f, 0.38f, 0.52f, 1.0f);
				}
				slotPanel.AddThemeStyleboxOverride("panel", slotStyle);

				var slotLbl = new Label
				{
					Text = hasLetter ? formedWord.Substring(i, 1) : "_",
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				slotLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
				slotLbl.AddThemeFontSizeOverride("font_size", 14);
				slotPanel.AddChild(slotLbl);

				_wsLetterSlotsBox.AddChild(slotPanel);
			}
		}

		if (_wsClearBtn != null)
		{
			_wsClearBtn.Disabled = _wsClickedPath.Count == 0 || _myFinished;
		}
		if (_wsConfirmBtn != null)
		{
			_wsConfirmBtn.Disabled = _wsClickedPath.Count == 0 || _myFinished;
			_wsConfirmBtn.Text = targetLen > 0
				? $"✔ Confirmar ({_wsClickedPath.Count}/{targetLen})"
				: "✔ Confirmar Palavra";
		}

		// Atualiza o checklist lateral de missões
		if (_wsChecklistLabel != null)
		{
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < words.Count; i++)
			{
				WordSearchGenerator.PlacedWord wInfo = words[i];
				string qPrompt = "";
				if (i < questions.Count)
				{
					Godot.Collections.Dictionary qDict = questions[i].AsGodotDictionary();
					qPrompt = qDict.TryGetValue("prompt", out Variant pv) ? pv.AsString() : "";
				}

				if (i < _myQuestionIdx)
				{
					string ans = i < _teamSubmittedAnswers.Count ? _teamSubmittedAnswers[i] : wInfo.Word;
					sb.AppendLine($"[color=#6ee787][b]✔ #{i + 1}: {ans}[/b] ({wInfo.DirectionName})[/color]");
				}
				else if (i == _myQuestionIdx && !_myFinished)
				{
					sb.AppendLine($"[color=#58d5ff][b]▶ #{i + 1} ({wInfo.Word.Length} letras — {wInfo.DirectionName}):[/b]\n{qPrompt}[/color]");
				}
				else
				{
					sb.AppendLine($"[color=#8b95a8]• #{i + 1} ({wInfo.Word.Length} letras): {qPrompt}[/color]");
				}
				sb.AppendLine();
			}
			_wsChecklistLabel.Text = sb.ToString();
		}
	}

	private void OnWordSearchCellGuiInput(InputEvent @event, int r, int c)
	{
		if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
		{
			return;
		}
		if (!_isWordSearchMode || _myFinished)
		{
			return;
		}

		int targetLen = (_myQuestionIdx < _wordSearchBoard.Words.Count)
			? _wordSearchBoard.Words[_myQuestionIdx].Word.Length
			: 12;

		var clickedPos = new Vector2I(r, c);

		// 1. Se a seleção está vazia, inicia no quadrado clicado
		if (_wsClickedPath.Count == 0)
		{
			_wsClickedPath.Add(clickedPos);
			RebuildWordSearchBoards("");
			return;
		}

		// 2. Se clicou no último quadrado selecionado, desfaz o último clique
		if (_wsClickedPath[^1] == clickedPos)
		{
			_wsClickedPath.RemoveAt(_wsClickedPath.Count - 1);
			RebuildWordSearchBoards("");
			return;
		}

		// 3. Se clicou em um quadrado anterior do próprio caminho, volta a seleção até ele
		int existingIdx = _wsClickedPath.IndexOf(clickedPos);
		if (existingIdx >= 0)
		{
			_wsClickedPath.RemoveRange(existingIdx + 1, _wsClickedPath.Count - existingIdx - 1);
			RebuildWordSearchBoards("");
			return;
		}

		// 4. Verifica se o clique está alinhado em linha reta (H, V ou Diagonal 45°) a partir do 1º quadrado
		Vector2I start = _wsClickedPath[0];
		int drTotal = r - start.X;
		int dcTotal = c - start.Y;
		int absR = Math.Abs(drTotal);
		int absC = Math.Abs(dcTotal);

		bool alignedWithStart = (drTotal == 0 || dcTotal == 0 || absR == absC);
		if (alignedWithStart)
		{
			int stepR = Math.Sign(drTotal);
			int stepC = Math.Sign(dcTotal);

			// Se já havia direção definida (Count >= 2), confere se mantém o mesmo sentido
			bool sameDirection = true;
			if (_wsClickedPath.Count >= 2)
			{
				int curDirR = _wsClickedPath[1].X - start.X;
				int curDirC = _wsClickedPath[1].Y - start.Y;
				if (stepR != curDirR || stepC != curDirC)
				{
					sameDirection = false;
				}
			}

			int steps = Math.Max(absR, absC);
			if (sameDirection && steps + 1 <= targetLen)
			{
				// Preenche todos os quadrados até o quadrado clicado (suporta clicar quadrado por quadrado OU direto no fim!)
				_wsClickedPath.Clear();
				for (int i = 0; i <= steps; i++)
				{
					_wsClickedPath.Add(new Vector2I(start.X + stepR * i, start.Y + stepC * i));
				}
				RebuildWordSearchBoards("");
				return;
			}
		}

		// 5. Caso contrário, inicia uma nova seleção a partir deste quadrado
		_wsClickedPath.Clear();
		_wsClickedPath.Add(clickedPos);
		RebuildWordSearchBoards("");
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
