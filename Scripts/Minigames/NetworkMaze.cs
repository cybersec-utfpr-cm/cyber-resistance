using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkMaze : Control
{
	private const string FairModeMenuScenePath =
		"res://Scenes/Interfaces/fair_mode_menu.tscn";
	private const int Columns = 17;
	private const int Rows = 11;
	private const float CellSize = 48.0f;
	private const float MoveInterval = 0.105f;
	private const float MalwareMoveInterval = 0.42f;
	private const float RoundDurationSeconds = 120.0f;
	private const float InvulnerabilitySeconds = 1.15f;

	private static readonly string[] Maze =
	{
		"#################",
		"#.....#.........#",
		"#####.###.#.###.#",
		"#...#...#.#...#.#",
		"###.###.#####.###",
		"#...#...#...#...#",
		"#.#.#.###.#.###.#",
		"#.#.#.#...#.....#",
		"#.###.#.#######.#",
		"#.......#.......#",
		"#################"
	};

	private static readonly Vector2I PlayerStart = new(1, 1);
	private static readonly Vector2I ServerPosition = new(15, 3);
	private static readonly Vector2I AuthKeyPosition = new(3, 7);
	private static readonly Vector2I EncryptionPosition = new(11, 9);
	private static readonly Vector2I FirewallPosition = new(15, 5);
	private static readonly Vector2 BoardOrigin = new(36.0f, 112.0f);

	private static readonly Color BackgroundColor = new(0.008f, 0.018f, 0.03f, 1.0f);
	private static readonly Color BoardColor = new(0.02f, 0.045f, 0.067f, 1.0f);
	private static readonly Color WallColor = new(0.045f, 0.12f, 0.16f, 1.0f);
	private static readonly Color WallEdgeColor = new(0.08f, 0.34f, 0.42f, 1.0f);
	private static readonly Color NetworkLineColor = new(0.08f, 0.25f, 0.30f, 0.78f);
	private static readonly Color NetworkNodeColor = new(0.14f, 0.48f, 0.55f, 0.75f);
	private static readonly Color PlayerColor = new(0.32f, 0.88f, 0.95f, 1.0f);
	private static readonly Color PlayerGlowColor = new(0.18f, 0.62f, 0.72f, 0.26f);
	private static readonly Color MalwareColor = new(0.96f, 0.24f, 0.31f, 1.0f);
	private static readonly Color MalwareGlowColor = new(0.9f, 0.12f, 0.18f, 0.22f);
	private static readonly Color ServerColor = new(0.31f, 0.88f, 0.56f, 1.0f);
	private static readonly Color KeyColor = new(0.98f, 0.78f, 0.26f, 1.0f);
	private static readonly Color EncryptionColor = new(0.38f, 0.58f, 0.98f, 1.0f);
	private static readonly Color FirewallLockedColor = new(0.95f, 0.36f, 0.24f, 1.0f);
	private static readonly Color FirewallOpenColor = new(0.28f, 0.82f, 0.58f, 1.0f);

	[Export] public NodePath TimeLabelPath { get; set; }
	[Export] public NodePath LivesLabelPath { get; set; }
	[Export] public NodePath ScoreLabelPath { get; set; }
	[Export] public NodePath KeyLabelPath { get; set; }
	[Export] public NodePath EncryptionLabelPath { get; set; }
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
	private Label _livesLabel;
	private Label _scoreLabel;
	private Label _keyLabel;
	private Label _encryptionLabel;
	private Label _statusLabel;
	private Control _tutorialOverlay;
	private Button _startButton;
	private Control _resultOverlay;
	private Label _resultTitle;
	private Label _resultSummary;
	private Button _retryButton;
	private Button _resultBackButton;
	private Button _backButton;

	private Vector2I _playerPosition;
	private Vector2 _playerRenderPosition;
	private readonly List<MalwareAgent> _malwareAgents = new();
	private bool _hasAuthKey;
	private bool _hasEncryption;
	private bool _running;
	private bool _finished;
	private int _lives;
	private int _hits;
	private float _elapsedSeconds;
	private float _moveCooldown;
	private float _malwareCooldown;
	private float _invulnerabilityRemaining;
	private float _pulse;
	private int _lastDisplayedSecond = -1;
	private string _statusMessage = "";
	private float _statusMessageRemaining;

	private sealed class MalwareAgent
	{
		public Vector2I Position;
		public Vector2I Direction;
		public Vector2 RenderPosition;

		public MalwareAgent(Vector2I position, Vector2I direction)
		{
			Position = position;
			Direction = direction;
			RenderPosition = GridToCenter(position);
		}
	}

	public override void _Ready()
	{
		AudioManager.Instance?.SetGameplayContext("fair_network_maze");
		BindUi();
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
		float frameDelta = (float)delta;
		_pulse += frameDelta;

		Vector2 target = GridToCenter(_playerPosition);
		_playerRenderPosition = _playerRenderPosition.MoveToward(
			target,
			620.0f * frameDelta
		);

		foreach (MalwareAgent malware in _malwareAgents)
		{
			Vector2 malwareTarget = GridToCenter(malware.Position);
			malware.RenderPosition = malware.RenderPosition.MoveToward(
				malwareTarget,
				360.0f * frameDelta
			);
		}

		if (_statusMessageRemaining > 0.0f)
		{
			_statusMessageRemaining -= frameDelta;
			if (_statusMessageRemaining <= 0.0f && _statusLabel != null)
				_statusLabel.Text = "Leve o pacote até o servidor.";
		}

		if (_running && !_finished)
		{
			_elapsedSeconds += frameDelta;
			_moveCooldown -= frameDelta;
			_malwareCooldown -= frameDelta;
			_invulnerabilityRemaining = Mathf.Max(
				0.0f,
				_invulnerabilityRemaining - frameDelta
			);

			HandlePlayerInput();

			if (_malwareCooldown <= 0.0f)
			{
				_malwareCooldown = MalwareMoveInterval;
				MoveMalware();
				CheckMalwareCollision();
			}

			if (_elapsedSeconds >= RoundDurationSeconds)
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
		}
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor);
		DrawBoardBackground();
		DrawNetworkPaths();
		DrawMazeWalls();
		DrawFirewall();
		DrawServer();

		if (!_hasAuthKey)
			DrawAuthKey();

		if (!_hasEncryption)
			DrawEncryptionPickup();

		DrawMalware();
		DrawPlayerPacket();
	}

	private void BindUi()
	{
		_timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
		_livesLabel = GetNodeOrNull<Label>(LivesLabelPath);
		_scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
		_keyLabel = GetNodeOrNull<Label>(KeyLabelPath);
		_encryptionLabel = GetNodeOrNull<Label>(EncryptionLabelPath);
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

	private void ConnectSignals()
	{
		if (_startButton != null)
			_startButton.Pressed += StartRound;
		if (_retryButton != null)
			_retryButton.Pressed += RetryRound;
		if (_resultBackButton != null)
			_resultBackButton.Pressed += ReturnToFairMode;
		if (_backButton != null)
			_backButton.Pressed += ReturnToFairMode;
	}

	private void ResetRound(bool showTutorial)
	{
		_playerPosition = PlayerStart;
		_playerRenderPosition = GridToCenter(PlayerStart);
		_hasAuthKey = false;
		_hasEncryption = false;
		_running = !showTutorial;
		_finished = false;
		_lives = 3;
		_hits = 0;
		_elapsedSeconds = 0.0f;
		_moveCooldown = 0.0f;
		_malwareCooldown = MalwareMoveInterval;
		_invulnerabilityRemaining = 0.0f;
		_lastDisplayedSecond = -1;
		_statusMessage = "";
		_statusMessageRemaining = 0.0f;

		_malwareAgents.Clear();
		_malwareAgents.Add(new MalwareAgent(new Vector2I(2, 3), Vector2I.Right));
		_malwareAgents.Add(new MalwareAgent(new Vector2I(10, 5), Vector2I.Right));
		_malwareAgents.Add(new MalwareAgent(new Vector2I(13, 7), Vector2I.Right));

		if (_tutorialOverlay != null)
			_tutorialOverlay.Visible = showTutorial;
		if (_resultOverlay != null)
			_resultOverlay.Visible = false;
		if (_statusLabel != null)
			_statusLabel.Text = "Leve o pacote até o servidor.";

		UpdateHud(force: true);

		if (showTutorial)
			_startButton?.GrabFocus();
	}

	private void StartRound()
	{
		if (_tutorialOverlay != null)
			_tutorialOverlay.Visible = false;

		_running = true;
		_elapsedSeconds = 0.0f;
		_moveCooldown = 0.0f;
		_malwareCooldown = MalwareMoveInterval;
		_backButton?.GrabFocus();
		AudioManager.Instance?.PlayInteraction();
	}

	private void RetryRound()
	{
		ResetRound(showTutorial: false);
		_running = true;
		AudioManager.Instance?.PlayInteraction();
	}

	private void ReturnToFairMode()
	{
		_running = false;
		AudioManager.Instance?.SetMenuContext();

		Error error = GetTree().ChangeSceneToFile(FairModeMenuScenePath);
		if (error != Error.Ok)
			GD.PrintErr($"NetworkMaze: falha ao voltar ao Modo Feira: {error}.");
	}

	private void HandlePlayerInput()
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
		TryMovePlayer(direction);
	}

	private void TryMovePlayer(Vector2I direction)
	{
		Vector2I next = _playerPosition + direction;

		if (IsWall(next))
		{
			ShowTemporaryStatus("Rota bloqueada. Procure outro caminho.", 1.0f);
			return;
		}

		if (next == FirewallPosition && !_hasAuthKey)
		{
			ShowTemporaryStatus(
				"Firewall bloqueado: encontre a chave de autenticação.",
				1.8f
			);
			AudioManager.Instance?.PlayError();
			return;
		}

		_playerPosition = next;
		AudioManager.Instance?.PlayFootstep();

		if (_playerPosition == AuthKeyPosition && !_hasAuthKey)
		{
			_hasAuthKey = true;
			ShowTemporaryStatus(
				"Chave obtida: o firewall agora reconhece seu pacote.",
				2.2f
			);
			AudioManager.Instance?.PlaySuccess();
		}

		if (_playerPosition == EncryptionPosition && !_hasEncryption)
		{
			_hasEncryption = true;
			ShowTemporaryStatus(
				"Criptografia ativada: você está protegido contra um malware.",
				2.4f
			);
			AudioManager.Instance?.PlaySuccess();
		}

		if (_playerPosition == FirewallPosition && _hasAuthKey)
		{
			ShowTemporaryStatus(
				"Firewall autenticado. Acesso permitido.",
				1.6f
			);
		}

		CheckMalwareCollision();

		if (_playerPosition == ServerPosition && !_finished)
			FinishRound(true, "Pacote entregue");
	}

	private void MoveMalware()
	{
		foreach (MalwareAgent malware in _malwareAgents)
		{
			Vector2I forward = malware.Position + malware.Direction;

			if (!CanMalwareEnter(forward))
			{
				malware.Direction = RotateClockwise(malware.Direction);
				forward = malware.Position + malware.Direction;

				if (!CanMalwareEnter(forward))
				{
					malware.Direction = -malware.Direction;
					forward = malware.Position + malware.Direction;
				}
			}

			if (CanMalwareEnter(forward))
				malware.Position = forward;
		}
	}

	private void CheckMalwareCollision()
	{
		if (!_running || _finished || _invulnerabilityRemaining > 0.0f)
			return;

		foreach (MalwareAgent malware in _malwareAgents)
		{
			if (malware.Position != _playerPosition)
				continue;

			_invulnerabilityRemaining = InvulnerabilitySeconds;
			malware.Direction = -malware.Direction;

			if (_hasEncryption)
			{
				_hasEncryption = false;
				ShowTemporaryStatus(
					"A criptografia absorveu o ataque do malware.",
					2.0f
				);
				AudioManager.Instance?.PlaySuccess();
				UpdateHud(force: true);
				return;
			}

			_lives--;
			_hits++;
			ShowTemporaryStatus(
				"Malware interceptou o pacote. Você perdeu uma vida.",
				2.0f
			);
			AudioManager.Instance?.PlayError();
			UpdateHud(force: true);

			if (_lives <= 0)
				FinishRound(false, "Pacote comprometido");

			return;
		}
	}

	private void FinishRound(bool success, string title)
	{
		if (_finished)
			return;

		_running = false;
		_finished = true;
		int score = CalculateScore(success);

		if (_resultTitle != null)
			_resultTitle.Text = success ? "CONEXÃO SEGURA" : "MISSÃO INTERROMPIDA";

		if (_resultSummary != null)
		{
			string keyStatus = _hasAuthKey ? "obtida" : "não obtida";
			string resultLine = success
				? "O pacote chegou ao servidor sem quebrar as regras da rede."
				: title + ". Tente outra rota e observe os malwares.";

			_resultSummary.Text =
				$"{resultLine}\n\n" +
				$"Pontuação: {score}\n" +
				$"Tempo: {FormatTime(_elapsedSeconds)}\n" +
				$"Vidas restantes: {_lives}\n" +
				$"Chave de autenticação: {keyStatus}";
		}

		if (_resultOverlay != null)
			_resultOverlay.Visible = true;

		if (success)
			AudioManager.Instance?.PlaySuccess();
		else
			AudioManager.Instance?.PlayError();

		_retryButton?.GrabFocus();
		UpdateHud(force: true);
	}

	private int CalculateScore(bool success)
	{
		int baseScore = success ? 5000 : 1200;
		int timePenalty = Mathf.RoundToInt(_elapsedSeconds * 18.0f);
		int hitPenalty = _hits * 450;
		int lifeBonus = _lives * 250;
		int keyBonus = _hasAuthKey ? 400 : 0;

		return Math.Max(
			0,
			baseScore - timePenalty - hitPenalty + lifeBonus + keyBonus
		);
	}

	private void UpdateHud(bool force = false)
	{
		int remainingSeconds = Math.Max(
			0,
			Mathf.CeilToInt(RoundDurationSeconds - _elapsedSeconds)
		);

		if (force || remainingSeconds != _lastDisplayedSecond)
		{
			_lastDisplayedSecond = remainingSeconds;
			if (_timeLabel != null)
				_timeLabel.Text = $"Tempo  {FormatTime(remainingSeconds)}";
		}

		if (_livesLabel != null)
			_livesLabel.Text = $"Vidas  {_lives}/3";
		if (_scoreLabel != null)
			_scoreLabel.Text = $"Pontos  {CalculateScore(success: true)}";
		if (_keyLabel != null)
			_keyLabel.Text = _hasAuthKey
				? "Chave de autenticação: OK"
				: "Chave de autenticação: pendente";
		if (_encryptionLabel != null)
			_encryptionLabel.Text = _hasEncryption
				? "Criptografia: protegida"
				: "Criptografia: sem proteção";
	}

	private void ShowTemporaryStatus(string message, float seconds)
	{
		_statusMessage = message;
		_statusMessageRemaining = seconds;

		if (_statusLabel != null)
			_statusLabel.Text = _statusMessage;
	}

	private static bool IsWall(Vector2I position)
	{
		if (
			position.X < 0 || position.X >= Columns ||
			position.Y < 0 || position.Y >= Rows
		)
		{
			return true;
		}

		return Maze[position.Y][position.X] == '#';
	}

	private static bool CanMalwareEnter(Vector2I position)
	{
		return !IsWall(position) && position != FirewallPosition;
	}

	private static Vector2I RotateClockwise(Vector2I direction)
	{
		return new Vector2I(-direction.Y, direction.X);
	}

	private static Vector2 GridToCenter(Vector2I gridPosition)
	{
		return BoardOrigin + new Vector2(
			(gridPosition.X + 0.5f) * CellSize,
			(gridPosition.Y + 0.5f) * CellSize
		);
	}

	private static string FormatTime(float seconds)
	{
		int wholeSeconds = Math.Max(0, Mathf.CeilToInt(seconds));
		int minutes = wholeSeconds / 60;
		int remainder = wholeSeconds % 60;
		return $"{minutes:00}:{remainder:00}";
	}

	private void DrawBoardBackground()
	{
		Rect2 boardRect = new(
			BoardOrigin - new Vector2(6.0f, 6.0f),
			new Vector2(Columns * CellSize + 12.0f, Rows * CellSize + 12.0f)
		);
		DrawRect(boardRect, BoardColor);
		DrawRect(boardRect, new Color(0.12f, 0.48f, 0.58f, 0.8f), false, 2.0f);
	}

	private void DrawNetworkPaths()
	{
		for (int y = 0; y < Rows; y++)
		{
			for (int x = 0; x < Columns; x++)
			{
				Vector2I cell = new(x, y);
				if (IsWall(cell))
					continue;

				Vector2 center = GridToCenter(cell);
				DrawCircle(center, 2.4f, NetworkNodeColor);

				Vector2I right = cell + Vector2I.Right;
				Vector2I down = cell + Vector2I.Down;

				if (!IsWall(right))
					DrawLine(center, GridToCenter(right), NetworkLineColor, 2.0f);
				if (!IsWall(down))
					DrawLine(center, GridToCenter(down), NetworkLineColor, 2.0f);
			}
		}
	}

	private void DrawMazeWalls()
	{
		for (int y = 0; y < Rows; y++)
		{
			for (int x = 0; x < Columns; x++)
			{
				if (Maze[y][x] != '#')
					continue;

				Vector2 topLeft = BoardOrigin + new Vector2(x * CellSize, y * CellSize);
				Rect2 wallRect = new(topLeft + new Vector2(2.0f, 2.0f), new Vector2(CellSize - 4.0f, CellSize - 4.0f));
				DrawRect(wallRect, WallColor);
				DrawRect(wallRect, WallEdgeColor, false, 1.0f);
			}
		}
	}

	private void DrawFirewall()
	{
		Vector2 center = GridToCenter(FirewallPosition);
		Color color = _hasAuthKey ? FirewallOpenColor : FirewallLockedColor;
		float half = CellSize * 0.31f;

		DrawCircle(center, half + 6.0f, new Color(color.R, color.G, color.B, 0.14f));
		DrawLine(center + new Vector2(-half, -half), center + new Vector2(-half, half), color, 4.0f);
		DrawLine(center + new Vector2(0.0f, -half), center + new Vector2(0.0f, half), color, 4.0f);
		DrawLine(center + new Vector2(half, -half), center + new Vector2(half, half), color, 4.0f);
		DrawLine(center + new Vector2(-half, -half), center + new Vector2(half, -half), color, 4.0f);
		DrawLine(center + new Vector2(-half, half), center + new Vector2(half, half), color, 4.0f);
	}

	private void DrawServer()
	{
		Vector2 center = GridToCenter(ServerPosition);
		float glow = 4.0f + (Mathf.Sin(_pulse * 3.0f) + 1.0f) * 2.0f;
		Rect2 outer = new(center - new Vector2(17.0f + glow, 19.0f + glow), new Vector2(34.0f + glow * 2.0f, 38.0f + glow * 2.0f));
		Rect2 rack = new(center - new Vector2(15.0f, 17.0f), new Vector2(30.0f, 34.0f));

		DrawRect(outer, new Color(ServerColor.R, ServerColor.G, ServerColor.B, 0.12f));
		DrawRect(rack, new Color(0.04f, 0.16f, 0.12f, 1.0f));
		DrawRect(rack, ServerColor, false, 2.0f);
		DrawLine(center + new Vector2(-9.0f, -7.0f), center + new Vector2(9.0f, -7.0f), ServerColor, 2.0f);
		DrawLine(center + new Vector2(-9.0f, 2.0f), center + new Vector2(9.0f, 2.0f), ServerColor, 2.0f);
		DrawCircle(center + new Vector2(9.0f, 10.0f), 2.5f, ServerColor);
	}

	private void DrawAuthKey()
	{
		Vector2 center = GridToCenter(AuthKeyPosition);
		float bob = Mathf.Sin(_pulse * 4.0f) * 3.0f;
		center.Y += bob;

		DrawCircle(center + new Vector2(-6.0f, 0.0f), 7.0f, new Color(0.08f, 0.07f, 0.02f, 1.0f));
		DrawCircle(center + new Vector2(-6.0f, 0.0f), 7.0f, KeyColor, false, 3.0f);
		DrawLine(center + new Vector2(1.0f, 0.0f), center + new Vector2(15.0f, 0.0f), KeyColor, 4.0f);
		DrawLine(center + new Vector2(10.0f, 0.0f), center + new Vector2(10.0f, 6.0f), KeyColor, 3.0f);
	}

	private void DrawEncryptionPickup()
	{
		Vector2 center = GridToCenter(EncryptionPosition);
		float radius = 12.0f + (Mathf.Sin(_pulse * 4.6f) + 1.0f) * 1.5f;

		DrawCircle(center, radius + 6.0f, new Color(EncryptionColor.R, EncryptionColor.G, EncryptionColor.B, 0.13f));
		DrawCircle(center, radius, new Color(0.04f, 0.07f, 0.16f, 1.0f));
		DrawCircle(center, radius, EncryptionColor, false, 3.0f);
		DrawLine(center + new Vector2(-5.0f, 0.0f), center + new Vector2(5.0f, 0.0f), EncryptionColor, 3.0f);
		DrawLine(center + new Vector2(0.0f, -5.0f), center + new Vector2(0.0f, 5.0f), EncryptionColor, 3.0f);
	}

	private void DrawMalware()
	{
		foreach (MalwareAgent malware in _malwareAgents)
		{
			Vector2 center = malware.RenderPosition;
			float pulseRadius = 17.0f + (Mathf.Sin(_pulse * 5.0f) + 1.0f) * 2.0f;
			DrawCircle(center, pulseRadius + 6.0f, MalwareGlowColor);
			DrawCircle(center, 13.0f, new Color(0.2f, 0.025f, 0.04f, 1.0f));
			DrawCircle(center, 13.0f, MalwareColor, false, 3.0f);

			for (int i = 0; i < 8; i++)
			{
				float angle = Mathf.Tau * i / 8.0f;
				Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
				DrawLine(center + dir * 13.0f, center + dir * 18.0f, MalwareColor, 2.0f);
			}

			DrawCircle(center + new Vector2(-4.0f, -2.0f), 2.2f, MalwareColor);
			DrawCircle(center + new Vector2(4.0f, -2.0f), 2.2f, MalwareColor);
			DrawLine(center + new Vector2(-5.0f, 5.0f), center + new Vector2(5.0f, 5.0f), MalwareColor, 2.0f);
		}
	}

	private void DrawPlayerPacket()
	{
		Vector2 center = _playerRenderPosition;
		bool blinkOff = _invulnerabilityRemaining > 0.0f && ((int)(_pulse * 12.0f) % 2 == 0);
		if (blinkOff)
			return;

		DrawCircle(center, 22.0f, PlayerGlowColor);
		Rect2 packet = new(center - new Vector2(14.0f, 10.0f), new Vector2(28.0f, 20.0f));
		DrawRect(packet, new Color(0.025f, 0.12f, 0.15f, 1.0f));
		DrawRect(packet, PlayerColor, false, 2.0f);
		DrawLine(center + new Vector2(-13.0f, -9.0f), center, PlayerColor, 2.0f);
		DrawLine(center, center + new Vector2(13.0f, -9.0f), PlayerColor, 2.0f);

		if (_hasEncryption)
			DrawCircle(center, 18.0f, EncryptionColor, false, 2.0f);
	}
}
