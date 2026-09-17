using Godot;
using System;
using System.Collections.Generic;

public partial class DataCenterRescue : Control
{
	private const string FairModeMenuScenePath =
		"res://Scenes/Interfaces/fair_mode_menu.tscn";
	private const int Columns = 17;
	private const int Rows = 11;
	private const int MaxIntegrity = 5;
	private const float CellSize = 48.0f;
	private const float MoveInterval = 0.105f;
	private const float RoundDurationSeconds = 105.0f;
	private const float BaseBotInterval = 0.40f;
	private const float InvulnerabilitySeconds = 1.4f;

	private static readonly string[] Map =
	{
		"#################",
		"#...............#",
		"#..###.....###..#",
		"#...............#",
		"#......###......#",
		"#...............#",
		"#..###.....###..#",
		"#...............#",
		"#......###......#",
		"#...............#",
		"#################"
	};

	private static readonly Vector2 BoardOrigin = new(36.0f, 112.0f);
	private static readonly Vector2I PlayerStart = new(8, 9);
	private static readonly Vector2I ControlTerminalPosition = new(8, 1);
	private static readonly Vector2I AccessCardPosition = new(8, 5);
	private static readonly Vector2I[] ServerPositions =
	{
		new(2, 2),
		new(14, 2),
		new(2, 5),
		new(14, 5),
		new(2, 8),
		new(14, 8)
	};

	private static readonly Color BackgroundColor = new(0.008f, 0.018f, 0.03f, 1.0f);
	private static readonly Color BoardColor = new(0.025f, 0.045f, 0.067f, 1.0f);
	private static readonly Color GridColor = new(0.08f, 0.16f, 0.2f, 0.48f);
	private static readonly Color WallColor = new(0.05f, 0.11f, 0.14f, 1.0f);
	private static readonly Color WallEdgeColor = new(0.12f, 0.34f, 0.38f, 1.0f);
	private static readonly Color PlayerColor = new(0.34f, 0.88f, 0.95f, 1.0f);
	private static readonly Color MalwareColor = new(0.98f, 0.28f, 0.34f, 1.0f);
	private static readonly Color BruteForceColor = new(0.98f, 0.72f, 0.24f, 1.0f);
	private static readonly Color PowerColor = new(0.72f, 0.5f, 0.96f, 1.0f);
	private static readonly Color SuccessColor = new(0.36f, 0.92f, 0.65f, 1.0f);
	private static readonly Color CardColor = new(0.98f, 0.8f, 0.3f, 1.0f);
	private static readonly Color BotColor = new(1.0f, 0.34f, 0.22f, 1.0f);

	private enum IncidentKind
	{
		Malware,
		BruteForce,
		PowerFailure
	}

	private enum ResponseAction
	{
		Quarantine,
		BlockAccess,
		RestorePower
	}

	private sealed class ServerState
	{
		public Vector2I Position;
		public IncidentKind Incident;
		public float Deadline;
		public bool RequiresLevel2;
		public bool Resolved;
	}

	private sealed class BotAgent
	{
		public Vector2I Position;
		public Vector2I Direction;
		public Vector2 RenderPosition;

		public BotAgent(Vector2I position, Vector2I direction)
		{
			Position = position;
			Direction = direction;
			RenderPosition = GridToCenter(position);
		}
	}

	[Export] public NodePath TimeLabelPath { get; set; }
	[Export] public NodePath IntegrityLabelPath { get; set; }
	[Export] public NodePath ResolvedLabelPath { get; set; }
	[Export] public NodePath AccessLabelPath { get; set; }
	[Export] public NodePath ScoreLabelPath { get; set; }
	[Export] public NodePath CriticalLabelPath { get; set; }
	[Export] public NodePath StatusLabelPath { get; set; }
	[Export] public NodePath TutorialOverlayPath { get; set; }
	[Export] public NodePath StartButtonPath { get; set; }
	[Export] public NodePath ResultOverlayPath { get; set; }
	[Export] public NodePath ResultTitlePath { get; set; }
	[Export] public NodePath ResultSummaryPath { get; set; }
	[Export] public NodePath RetryButtonPath { get; set; }
	[Export] public NodePath ResultBackButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }

	private Label _timeLabel;
	private Label _integrityLabel;
	private Label _resolvedLabel;
	private Label _accessLabel;
	private Label _scoreLabel;
	private Label _criticalLabel;
	private Label _statusLabel;
	private Control _tutorialOverlay;
	private Button _startButton;
	private Control _resultOverlay;
	private Label _resultTitle;
	private Label _resultSummary;
	private Button _retryButton;
	private Button _resultBackButton;
	private Button _backButton;

	private readonly List<ServerState> _servers = new();
	private readonly List<BotAgent> _bots = new();
	private readonly RandomNumberGenerator _rng = new();
	private Vector2I _playerPosition;
	private Vector2 _playerRenderPosition;
	private bool _running;
	private bool _finished;
	private bool _hasLevel2Card;
	private int _resolvedCount;
	private int _integrity;
	private int _score;
	private int _wrongActions;
	private int _expiredIncidents;
	private int _botHits;
	private float _timeRemaining;
	private float _moveCooldown;
	private float _botCooldown;
	private float _invulnerabilityRemaining;
	private float _statusRemaining;
	private float _pulse;
	private int _lastDisplayedSecond = -1;

	public override void _Ready()
	{
		AudioManager.Instance?.SetGameplayContext("fair_data_center_rescue");
		_rng.Randomize();
		BindUi();

		if (!HasRequiredNodes())
		{
			GD.PrintErr("DataCenterRescue: estrutura da interface não encontrada.");
			return;
		}

		ConnectSignals();
		ResetRound(showTutorial: true);
		SetProcess(true);
		QueueRedraw();
	}

	public override void _ExitTree()
	{
		if (_startButton != null)
			_startButton.Pressed -= StartRound;
		if (_retryButton != null)
			_retryButton.Pressed -= RetryRound;
		if (_resultBackButton != null)
			_resultBackButton.Pressed -= ReturnToFairMode;
		if (_backButton != null)
			_backButton.Pressed -= ReturnToFairMode;
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_pulse += step;
		_playerRenderPosition = _playerRenderPosition.MoveToward(
			GridToCenter(_playerPosition),
			620.0f * step
		);

		foreach (BotAgent bot in _bots)
		{
			bot.RenderPosition = bot.RenderPosition.MoveToward(
				GridToCenter(bot.Position),
				400.0f * step
			);
		}

		if (_statusRemaining > 0.0f)
		{
			_statusRemaining -= step;
			if (_statusRemaining <= 0.0f)
				_statusLabel.Text = GetDefaultStatus();
		}
		else if (_running && !_finished)
		{
			_statusLabel.Text = GetDefaultStatus();
		}

		if (_running && !_finished)
		{
			_timeRemaining = Math.Max(0.0f, _timeRemaining - step);
			_moveCooldown -= step;
			_botCooldown -= step;
			_invulnerabilityRemaining = Math.Max(0.0f, _invulnerabilityRemaining - step);
			HandleMovement();
			UpdateIncidentDeadlines(step);

			if (_botCooldown <= 0.0f)
			{
				_botCooldown = GetBotInterval();
				MoveBots();
				CheckBotCollision();
			}

			if (_timeRemaining <= 0.0f && _running)
				FinishRound(false, "Tempo esgotado");

			UpdateHud();
		}

		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && !@event.IsEcho())
		{
			ReturnToFairMode();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_running || @event is not InputEventKey keyEvent ||
			!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (IsKey(keyEvent, Key.Q))
			RespondToNearestServer(ResponseAction.Quarantine);
		else if (IsKey(keyEvent, Key.B))
			RespondToNearestServer(ResponseAction.BlockAccess);
		else if (IsKey(keyEvent, Key.P))
			RespondToNearestServer(ResponseAction.RestorePower);
		else
			return;

		GetViewport().SetInputAsHandled();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor);
		DrawBoard();
		DrawWalls();
		DrawControlTerminal();

		for (int i = 0; i < _servers.Count; i++)
			DrawServer(i, _servers[i]);

		if (!_hasLevel2Card)
			DrawAccessCard();

		DrawBots();
		DrawPlayer();
	}

	private static bool IsKey(InputEventKey keyEvent, Key key)
	{
		return keyEvent.Keycode == key || keyEvent.PhysicalKeycode == key;
	}

	private void BindUi()
	{
		_timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
		_integrityLabel = GetNodeOrNull<Label>(IntegrityLabelPath);
		_resolvedLabel = GetNodeOrNull<Label>(ResolvedLabelPath);
		_accessLabel = GetNodeOrNull<Label>(AccessLabelPath);
		_scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
		_criticalLabel = GetNodeOrNull<Label>(CriticalLabelPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
		_tutorialOverlay = GetNodeOrNull<Control>(TutorialOverlayPath);
		_startButton = GetNodeOrNull<Button>(StartButtonPath);
		_resultOverlay = GetNodeOrNull<Control>(ResultOverlayPath);
		_resultTitle = GetNodeOrNull<Label>(ResultTitlePath);
		_resultSummary = GetNodeOrNull<Label>(ResultSummaryPath);
		_retryButton = GetNodeOrNull<Button>(RetryButtonPath);
		_resultBackButton = GetNodeOrNull<Button>(ResultBackButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
	}

	private bool HasRequiredNodes()
	{
		return _timeLabel != null && _integrityLabel != null && _resolvedLabel != null &&
			_accessLabel != null && _scoreLabel != null && _criticalLabel != null &&
			_statusLabel != null && _tutorialOverlay != null && _startButton != null &&
			_resultOverlay != null && _resultTitle != null && _resultSummary != null &&
			_retryButton != null && _resultBackButton != null && _backButton != null;
	}

	private void ConnectSignals()
	{
		_startButton.Pressed += StartRound;
		_retryButton.Pressed += RetryRound;
		_resultBackButton.Pressed += ReturnToFairMode;
		_backButton.Pressed += ReturnToFairMode;
	}

	private void ResetRound(bool showTutorial)
	{
		_playerPosition = PlayerStart;
		_playerRenderPosition = GridToCenter(PlayerStart);
		_running = !showTutorial;
		_finished = false;
		_hasLevel2Card = false;
		_resolvedCount = 0;
		_integrity = MaxIntegrity;
		_score = 0;
		_wrongActions = 0;
		_expiredIncidents = 0;
		_botHits = 0;
		_timeRemaining = RoundDurationSeconds;
		_moveCooldown = 0.0f;
		_botCooldown = BaseBotInterval;
		_invulnerabilityRemaining = 0.0f;
		_statusRemaining = 0.0f;
		_lastDisplayedSecond = -1;

		BuildServers();
		BuildBots();
		_tutorialOverlay.Visible = showTutorial;
		_resultOverlay.Visible = false;
		_statusLabel.Text = "[WASD / SETAS] Aproxime-se de um servidor. O comando aparecerá aqui.";
		UpdateHud(force: true);

		if (showTutorial)
			_startButton.GrabFocus();
	}

	private void BuildServers()
	{
		var incidents = new List<IncidentKind>
		{
			IncidentKind.Malware,
			IncidentKind.Malware,
			IncidentKind.BruteForce,
			IncidentKind.BruteForce,
			IncidentKind.PowerFailure,
			IncidentKind.PowerFailure
		};

		for (int i = incidents.Count - 1; i > 0; i--)
		{
			int swapIndex = _rng.RandiRange(0, i);
			IncidentKind temporary = incidents[i];
			incidents[i] = incidents[swapIndex];
			incidents[swapIndex] = temporary;
		}

		_servers.Clear();
		for (int i = 0; i < ServerPositions.Length; i++)
		{
			_servers.Add(new ServerState
			{
				Position = ServerPositions[i],
				Incident = incidents[i],
				Deadline = _rng.RandfRange(25.0f, 38.0f),
				RequiresLevel2 = i >= 3,
				Resolved = false
			});
		}
	}

	private void BuildBots()
	{
		_bots.Clear();
		_bots.Add(new BotAgent(new Vector2I(4, 3), Vector2I.Right));
		_bots.Add(new BotAgent(new Vector2I(12, 7), Vector2I.Left));
		_bots.Add(new BotAgent(new Vector2I(8, 3), Vector2I.Down));
	}

	private void StartRound()
	{
		_tutorialOverlay.Visible = false;
		_running = true;
		_timeRemaining = RoundDurationSeconds;
		GetViewport().GuiReleaseFocus();
		AudioManager.Instance?.PlayInteraction();
	}

	private void RetryRound()
	{
		ResetRound(showTutorial: false);
		_running = true;
		GetViewport().GuiReleaseFocus();
		AudioManager.Instance?.PlayInteraction();
	}

	private void ReturnToFairMode()
	{
		_running = false;
		AudioManager.Instance?.SetMenuContext();
		Error error = GetTree().ChangeSceneToFile(FairModeMenuScenePath);
		if (error != Error.Ok)
			GD.PrintErr($"DataCenterRescue: falha ao voltar ao Modo Feira: {error}.");
	}

	private void HandleMovement()
	{
		if (_moveCooldown > 0.0f)
			return;

		Vector2I direction = Vector2I.Zero;
		if (Input.IsActionPressed("ui_up"))
			direction = Vector2I.Up;
		else if (Input.IsActionPressed("ui_down"))
			direction = Vector2I.Down;
		else if (Input.IsActionPressed("ui_left"))
			direction = Vector2I.Left;
		else if (Input.IsActionPressed("ui_right"))
			direction = Vector2I.Right;

		if (direction == Vector2I.Zero)
			return;

		_moveCooldown = MoveInterval;
		Vector2I next = _playerPosition + direction;
		if (IsWall(next))
			return;

		_playerPosition = next;
		if (!_hasLevel2Card && _playerPosition == AccessCardPosition)
		{
			_hasLevel2Card = true;
			_score += 300;
			AudioManager.Instance?.PlaySuccess();
			ShowTemporaryStatus("CARTÃO NÍVEL 2 OBTIDO — todos os setores foram liberados.", 2.0f);
		}

		if (_playerPosition == ControlTerminalPosition)
		{
			if (_resolvedCount == _servers.Count)
				FinishRound(true, "Todos os sistemas foram estabilizados");
			else
				ShowTemporaryStatus($"Ainda existem {_servers.Count - _resolvedCount} incidentes ativos.", 1.5f);
		}

		ShowNearbyServerHint();
		CheckBotCollision();
	}

	private void ShowNearbyServerHint()
	{
		int index = FindNearestServer(includeResolved: false);
		if (index < 0)
			return;
		ServerState server = _servers[index];
		if (server.RequiresLevel2 && !_hasLevel2Card)
		{
			ShowTemporaryStatus(
				$"SERVIDOR {index + 1} BLOQUEADO — pegue o CARTÃO AMARELO no centro do mapa.",
				1.5f
			);
			return;
		}

		ShowTemporaryStatus(
			GetResponsePrompt(index, server),
			1.5f
		);
	}

	private static string GetResponsePrompt(int index, ServerState server)
	{
		return server.Incident switch
		{
			IncidentKind.Malware =>
				$"SERVIDOR {index + 1}: MALWARE — aperte [Q] para QUARENTENA.",
			IncidentKind.BruteForce =>
				$"SERVIDOR {index + 1}: BRUTE FORCE — aperte [B] para BLOQUEAR ACESSO.",
			_ =>
				$"SERVIDOR {index + 1}: FALHA DE ENERGIA — aperte [P] para RESTAURAR."
		};
	}

	private void RespondToNearestServer(ResponseAction action)
	{
		int index = FindNearestServer(includeResolved: true);
		if (index < 0)
		{
			ShowTemporaryStatus("Nenhum servidor ao alcance. Aproxime-se de um rack numerado.", 1.5f);
			return;
		}

		ServerState server = _servers[index];
		if (server.Resolved)
		{
			ShowTemporaryStatus($"Servidor {index + 1} já está estabilizado.", 1.2f);
			return;
		}

		if (server.RequiresLevel2 && !_hasLevel2Card)
		{
			ShowTemporaryStatus("ACESSO NEGADO — encontre o cartão amarelo de nível 2.", 1.8f);
			AudioManager.Instance?.PlayError();
			return;
		}

		if (IsCorrectResponse(server.Incident, action))
		{
			server.Resolved = true;
			_resolvedCount++;
			_score += 600 + Mathf.RoundToInt(server.Deadline * 14.0f);
			AudioManager.Instance?.PlaySuccess();
			string nextInstruction = _resolvedCount == _servers.Count
				? " Todos resolvidos: retorne ao terminal verde!"
				: " Os bots ficaram mais rápidos.";
			ShowTemporaryStatus(
				$"SERVIDOR {index + 1} ESTABILIZADO!{nextInstruction}",
				2.0f
			);
		}
		else
		{
			_wrongActions++;
			_timeRemaining = Math.Max(0.0f, _timeRemaining - 7.0f);
			server.Deadline = Math.Max(3.0f, server.Deadline - 5.0f);
			_score = Math.Max(0, _score - 180);
			AudioManager.Instance?.PlayError();
			ShowTemporaryStatus(
				$"RESPOSTA ERRADA no servidor {index + 1}: -7 segundos e incidente agravado!",
				2.2f
			);
		}

		UpdateHud(force: true);
	}

	private static bool IsCorrectResponse(IncidentKind incident, ResponseAction action)
	{
		return incident switch
		{
			IncidentKind.Malware => action == ResponseAction.Quarantine,
			IncidentKind.BruteForce => action == ResponseAction.BlockAccess,
			_ => action == ResponseAction.RestorePower
		};
	}

	private int FindNearestServer(bool includeResolved)
	{
		for (int i = 0; i < _servers.Count; i++)
		{
			ServerState server = _servers[i];
			if (!includeResolved && server.Resolved)
				continue;
			Vector2I delta = server.Position - _playerPosition;
			if (Math.Abs(delta.X) + Math.Abs(delta.Y) <= 1)
				return i;
		}
		return -1;
	}

	private void UpdateIncidentDeadlines(float step)
	{
		float pressure = 1.0f + _resolvedCount * 0.12f;
		for (int i = 0; i < _servers.Count; i++)
		{
			ServerState server = _servers[i];
			if (server.Resolved)
				continue;

			server.Deadline -= step * pressure;
			if (server.Deadline > 0.0f)
				continue;

			_integrity--;
			_expiredIncidents++;
			server.Deadline = _rng.RandfRange(14.0f, 19.0f);
			AudioManager.Instance?.PlayError();
			ShowTemporaryStatus(
				$"INCIDENTE CRÍTICO no servidor {i + 1}! Integridade reduzida.",
				2.0f
			);

			if (_integrity <= 0)
			{
				FinishRound(false, "Falhas não tratadas derrubaram o Data Center");
				return;
			}
		}
	}

	private float GetBotInterval()
	{
		return Math.Max(0.23f, BaseBotInterval - _resolvedCount * 0.025f);
	}

	private void MoveBots()
	{
		foreach (BotAgent bot in _bots)
		{
			int distance = ManhattanDistance(bot.Position, _playerPosition);
			bool chase = _resolvedCount >= 3 || distance <= 4 + _resolvedCount;
			Vector2I direction = chase
				? ChooseChaseDirection(bot.Position)
				: bot.Direction;

			Vector2I next = bot.Position + direction;
			if (IsWall(next))
			{
				direction = new Vector2I(-direction.Y, direction.X);
				next = bot.Position + direction;
				if (IsWall(next))
				{
					direction = -bot.Direction;
					next = bot.Position + direction;
				}
			}

			if (!IsWall(next))
			{
				bot.Position = next;
				bot.Direction = direction;
			}
		}
	}

	private Vector2I ChooseChaseDirection(Vector2I from)
	{
		Vector2I delta = _playerPosition - from;
		Vector2I primary = Math.Abs(delta.X) >= Math.Abs(delta.Y)
			? new Vector2I(Math.Sign(delta.X), 0)
			: new Vector2I(0, Math.Sign(delta.Y));
		Vector2I secondary = primary.X != 0
			? new Vector2I(0, Math.Sign(delta.Y))
			: new Vector2I(Math.Sign(delta.X), 0);

		if (primary != Vector2I.Zero && !IsWall(from + primary))
			return primary;
		if (secondary != Vector2I.Zero && !IsWall(from + secondary))
			return secondary;
		return Vector2I.Right;
	}

	private void CheckBotCollision()
	{
		if (_invulnerabilityRemaining > 0.0f || !_running)
			return;

		foreach (BotAgent bot in _bots)
		{
			if (bot.Position != _playerPosition)
				continue;

			_invulnerabilityRemaining = InvulnerabilitySeconds;
			_integrity--;
			_botHits++;
			_timeRemaining = Math.Max(0.0f, _timeRemaining - 5.0f);
			_score = Math.Max(0, _score - 120);
			_playerPosition = PlayerStart;
			_playerRenderPosition = GridToCenter(PlayerStart);
			AudioManager.Instance?.PlayError();
			ShowTemporaryStatus("BOT INFECTADO: integridade perdida, -5 segundos e retorno ao início!", 2.2f);
			if (_integrity <= 0)
				FinishRound(false, "Bots infectados comprometeram o Data Center");
			UpdateHud(force: true);
			break;
		}
	}

	private string GetDefaultStatus()
	{
		if (_resolvedCount == _servers.Count)
			return "[OBJETIVO] Todos os servidores estão seguros — volte ao TERMINAL VERDE.";

		int index = FindNearestServer(includeResolved: true);
		if (index >= 0)
		{
			ServerState server = _servers[index];
			if (server.Resolved)
				return $"SERVIDOR {index + 1} já foi resolvido. Procure outro rack numerado.";
			if (server.RequiresLevel2 && !_hasLevel2Card)
				return $"SERVIDOR {index + 1} BLOQUEADO — pegue o CARTÃO AMARELO no centro do mapa.";
			return GetResponsePrompt(index, server);
		}

		return "[WASD / SETAS] Aproxime-se de um servidor. O comando aparecerá aqui.";
	}

	private void ShowTemporaryStatus(string message, float duration)
	{
		_statusLabel.Text = message;
		_statusRemaining = duration;
	}

	private void FinishRound(bool success, string reason)
	{
		if (_finished)
			return;

		_running = false;
		_finished = true;
		if (success)
		{
			_score += Mathf.RoundToInt(_timeRemaining * 20.0f) + _integrity * 250;
			AudioManager.Instance?.PlaySuccess();
		}
		else
		{
			AudioManager.Instance?.PlayError();
		}
		FairModeProgress.RecordResult(
			"data_center_rescue",
			"Resgate no Data Center",
			success,
			_score
		);

		_resultTitle.Text = success ? "DATA CENTER PROTEGIDO" : "DATA CENTER COMPROMETIDO";
		string lesson = success
			? "Você priorizou incidentes, usou a resposta adequada e preservou a operação."
			: "Resposta a incidentes exige rapidez, mas aplicar a ação errada também piora o problema.";
		_resultSummary.Text =
			$"{reason}\n\n" +
			$"Pontuação: {_score}\n" +
			$"Servidores recuperados: {_resolvedCount}/6\n" +
			$"Integridade final: {Math.Max(0, _integrity)}/{MaxIntegrity}\n" +
			$"Respostas erradas: {_wrongActions}\n" +
			$"Incidentes críticos: {_expiredIncidents}\n" +
			$"Colisões com bots: {_botHits}\n" +
			$"Tempo restante: {FormatTime(_timeRemaining)}\n\n" +
			$"LIÇÃO: {lesson}";
		_resultOverlay.Visible = true;
		_retryButton.GrabFocus();
		UpdateHud(force: true);
	}

	private void UpdateHud(bool force = false)
	{
		int displayedSecond = Mathf.CeilToInt(_timeRemaining);
		if (force || displayedSecond != _lastDisplayedSecond)
		{
			_lastDisplayedSecond = displayedSecond;
			_timeLabel.Text = $"Tempo  {FormatTime(_timeRemaining)}";
		}

		_integrityLabel.Text = $"Integridade  {Math.Max(0, _integrity)}/{MaxIntegrity}";
		_integrityLabel.Modulate = _integrity >= 4 ? SuccessColor :
			_integrity >= 2 ? BruteForceColor : MalwareColor;
		_resolvedLabel.Text = $"Recuperados  {_resolvedCount}/6";
		_accessLabel.Text = _hasLevel2Card ? "Acesso  NÍVEL 2" : "Acesso  NÍVEL 1";
		_accessLabel.Modulate = _hasLevel2Card ? SuccessColor : CardColor;
		_scoreLabel.Text = $"Pontos  {_score}";

		int criticalIndex = -1;
		float lowestDeadline = float.MaxValue;
		for (int i = 0; i < _servers.Count; i++)
		{
			if (!_servers[i].Resolved && _servers[i].Deadline < lowestDeadline)
			{
				lowestDeadline = _servers[i].Deadline;
				criticalIndex = i;
			}
		}
		_criticalLabel.Text = criticalIndex < 0
			? "Nenhum incidente ativo"
			: $"MAIS URGENTE: servidor {criticalIndex + 1} • {Mathf.CeilToInt(lowestDeadline)}s";
		_criticalLabel.Modulate = lowestDeadline <= 8.0f ? MalwareColor : BruteForceColor;
	}

	private static string GetIncidentName(IncidentKind incident)
	{
		return incident switch
		{
			IncidentKind.Malware => "MALWARE",
			IncidentKind.BruteForce => "BRUTE FORCE",
			_ => "FALHA DE ENERGIA"
		};
	}

	private static Color GetIncidentColor(IncidentKind incident)
	{
		return incident switch
		{
			IncidentKind.Malware => MalwareColor,
			IncidentKind.BruteForce => BruteForceColor,
			_ => PowerColor
		};
	}

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Math.Max(0, Mathf.CeilToInt(seconds));
		return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
	}

	private static int ManhattanDistance(Vector2I first, Vector2I second)
	{
		return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
	}

	private static bool IsWall(Vector2I cell)
	{
		if (cell.X < 0 || cell.X >= Columns || cell.Y < 0 || cell.Y >= Rows)
			return true;
		return Map[cell.Y][cell.X] == '#';
	}

	private static Vector2 GridToCenter(Vector2I cell)
	{
		return BoardOrigin + new Vector2(
			(cell.X + 0.5f) * CellSize,
			(cell.Y + 0.5f) * CellSize
		);
	}

	private static Rect2 CellRect(Vector2I cell, float inset = 0.0f)
	{
		Vector2 topLeft = BoardOrigin + new Vector2(cell.X * CellSize, cell.Y * CellSize);
		return new Rect2(
			topLeft + new Vector2(inset, inset),
			new Vector2(CellSize - inset * 2.0f, CellSize - inset * 2.0f)
		);
	}

	private void DrawBoard()
	{
		Rect2 board = new(BoardOrigin, new Vector2(Columns * CellSize, Rows * CellSize));
		DrawRect(board, BoardColor);
		DrawRect(board, new Color(0.12f, 0.48f, 0.58f, 0.8f), false, 2.0f);
		for (int x = 1; x < Columns; x++)
		{
			float px = BoardOrigin.X + x * CellSize;
			DrawLine(new Vector2(px, BoardOrigin.Y), new Vector2(px, BoardOrigin.Y + Rows * CellSize), GridColor, 1.0f);
		}
		for (int y = 1; y < Rows; y++)
		{
			float py = BoardOrigin.Y + y * CellSize;
			DrawLine(new Vector2(BoardOrigin.X, py), new Vector2(BoardOrigin.X + Columns * CellSize, py), GridColor, 1.0f);
		}
	}

	private void DrawWalls()
	{
		for (int y = 0; y < Rows; y++)
		{
			for (int x = 0; x < Columns; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsWall(cell))
					continue;
				Rect2 rect = CellRect(cell, 2.0f);
				DrawRect(rect, WallColor);
				DrawRect(rect, WallEdgeColor, false, 1.0f);
			}
		}
	}

	private void DrawServer(int index, ServerState server)
	{
		Vector2 center = GridToCenter(server.Position);
		Color color = server.Resolved ? SuccessColor : GetIncidentColor(server.Incident);
		float urgencyPulse = server.Resolved ? 0.0f :
			Mathf.Clamp((10.0f - server.Deadline) / 10.0f, 0.0f, 1.0f) *
			(3.0f + Mathf.Sin(_pulse * 8.0f) * 2.0f);
		DrawCircle(center, 22.0f + urgencyPulse, new Color(color.R, color.G, color.B, 0.13f));
		DrawRect(new Rect2(center + new Vector2(-16, -17), new Vector2(32, 34)), new Color(0.025f, 0.07f, 0.09f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-16, -17), new Vector2(32, 34)), color, false, 2.0f);
		DrawLine(center + new Vector2(-10, -7), center + new Vector2(10, -7), color, 2.0f);
		DrawLine(center + new Vector2(-10, 2), center + new Vector2(10, 2), color, 2.0f);
		for (int dot = 0; dot <= index; dot++)
			DrawCircle(center + new Vector2(-12 + (dot % 4) * 7, 11 - (dot / 4) * 6), 1.7f, color);

		if (server.RequiresLevel2 && !_hasLevel2Card && !server.Resolved)
		{
			DrawRect(new Rect2(center + new Vector2(-7, -3), new Vector2(14, 12)), CardColor, false, 2.0f);
			DrawCircle(center + new Vector2(0, -3), 6.0f, CardColor, false, 2.0f);
		}
	}

	private void DrawAccessCard()
	{
		Vector2 center = GridToCenter(AccessCardPosition);
		DrawCircle(center, 21.0f, new Color(CardColor.R, CardColor.G, CardColor.B, 0.13f));
		DrawRect(new Rect2(center + new Vector2(-15, -10), new Vector2(30, 20)), new Color(0.16f, 0.12f, 0.03f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-15, -10), new Vector2(30, 20)), CardColor, false, 2.0f);
		DrawCircle(center + new Vector2(-8, 0), 4.0f, CardColor);
		DrawLine(center + new Vector2(0, -3), center + new Vector2(10, -3), CardColor, 2.0f);
		DrawLine(center + new Vector2(0, 4), center + new Vector2(7, 4), CardColor, 2.0f);
	}

	private void DrawControlTerminal()
	{
		Vector2 center = GridToCenter(ControlTerminalPosition);
		Color color = _resolvedCount == _servers.Count ? SuccessColor : new Color(0.36f, 0.46f, 0.5f, 1.0f);
		DrawCircle(center, 24.0f, new Color(color.R, color.G, color.B, 0.12f));
		DrawRect(new Rect2(center + new Vector2(-17, -14), new Vector2(34, 24)), new Color(0.025f, 0.09f, 0.08f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-17, -14), new Vector2(34, 24)), color, false, 2.0f);
		DrawLine(center + new Vector2(0, 10), center + new Vector2(0, 17), color, 3.0f);
		DrawLine(center + new Vector2(-9, 17), center + new Vector2(9, 17), color, 3.0f);
	}

	private void DrawBots()
	{
		foreach (BotAgent bot in _bots)
		{
			Vector2 center = bot.RenderPosition;
			DrawCircle(center, 19.0f, new Color(BotColor.R, BotColor.G, BotColor.B, 0.13f));
			DrawRect(new Rect2(center + new Vector2(-12, -10), new Vector2(24, 20)), new Color(0.18f, 0.035f, 0.02f, 1.0f));
			DrawRect(new Rect2(center + new Vector2(-12, -10), new Vector2(24, 20)), BotColor, false, 2.0f);
			DrawCircle(center + new Vector2(-5, -2), 2.5f, BotColor);
			DrawCircle(center + new Vector2(5, -2), 2.5f, BotColor);
			DrawLine(center + new Vector2(-6, 6), center + new Vector2(6, 6), BotColor, 2.0f);
		}
	}

	private void DrawPlayer()
	{
		if (_invulnerabilityRemaining > 0.0f && ((int)(_pulse * 12.0f) % 2 == 0))
			return;
		Vector2 center = _playerRenderPosition;
		DrawCircle(center, 22.0f, new Color(PlayerColor.R, PlayerColor.G, PlayerColor.B, 0.13f));
		DrawCircle(center + new Vector2(0, -9), 7.0f, PlayerColor);
		DrawLine(center + new Vector2(0, -2), center + new Vector2(0, 13), PlayerColor, 5.0f);
		DrawLine(center + new Vector2(0, 3), center + new Vector2(-10, 8), PlayerColor, 3.0f);
		DrawLine(center + new Vector2(0, 3), center + new Vector2(10, 8), PlayerColor, 3.0f);
		DrawLine(center + new Vector2(0, 13), center + new Vector2(-8, 19), PlayerColor, 3.0f);
		DrawLine(center + new Vector2(0, 13), center + new Vector2(8, 19), PlayerColor, 3.0f);
	}
}
