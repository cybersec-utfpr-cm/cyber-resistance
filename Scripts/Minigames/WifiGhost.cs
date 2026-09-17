using Godot;
using System;
using System.Collections.Generic;

public partial class WifiGhost : Control
{
	private const string FairModeMenuScenePath =
		"res://Scenes/Interfaces/fair_mode_menu.tscn";
	private const int Columns = 17;
	private const int Rows = 11;
	private const float CellSize = 48.0f;
	private const float MoveInterval = 0.105f;
	private const float RoundDurationSeconds = 90.0f;
	private const float BaseSnifferInterval = 0.43f;
	private const float InvulnerabilitySeconds = 1.4f;

	private static readonly string[] Map =
	{
		"#################",
		"#...............#",
		"#..###.....###..#",
		"#...............#",
		"#.....###.......#",
		"#........###....#",
		"#..###..........#",
		"#.......###.....#",
		"#...............#",
		"#...............#",
		"#################"
	};

	private static readonly Vector2 BoardOrigin = new(36.0f, 112.0f);
	private static readonly Vector2I PlayerStart = new(8, 9);
	private static readonly Vector2I TerminalPosition = new(8, 1);
	private static readonly Vector2I VpnPosition = new(8, 5);
	private static readonly Vector2I[] AccessPointPositions =
	{
		new(2, 1),
		new(14, 1),
		new(2, 9),
		new(14, 9)
	};

	private static readonly Color BackgroundColor = new(0.008f, 0.018f, 0.03f, 1.0f);
	private static readonly Color BoardColor = new(0.025f, 0.045f, 0.067f, 1.0f);
	private static readonly Color GridColor = new(0.08f, 0.16f, 0.2f, 0.48f);
	private static readonly Color WallColor = new(0.05f, 0.11f, 0.14f, 1.0f);
	private static readonly Color WallEdgeColor = new(0.12f, 0.34f, 0.38f, 1.0f);
	private static readonly Color PlayerColor = new(0.34f, 0.88f, 0.95f, 1.0f);
	private static readonly Color NeutralApColor = new(0.98f, 0.78f, 0.26f, 1.0f);
	private static readonly Color ScannedApColor = new(0.42f, 0.78f, 1.0f, 1.0f);
	private static readonly Color DangerColor = new(0.98f, 0.28f, 0.34f, 1.0f);
	private static readonly Color SuccessColor = new(0.36f, 0.92f, 0.65f, 1.0f);
	private static readonly Color VpnColor = new(0.72f, 0.5f, 0.96f, 1.0f);

	private sealed class AccessPointState
	{
		public Vector2I Position;
		public string Ssid;
		public string Evidence;
		public bool Legitimate;
		public bool Scanned;
		public bool Rejected;
	}

	private sealed class SnifferAgent
	{
		public Vector2I Position;
		public Vector2I Direction;
		public Vector2 RenderPosition;

		public SnifferAgent(Vector2I position, Vector2I direction)
		{
			Position = position;
			Direction = direction;
			RenderPosition = GridToCenter(position);
		}
	}

	[Export] public NodePath TimeLabelPath { get; set; }
	[Export] public NodePath DataLabelPath { get; set; }
	[Export] public NodePath ScannerLabelPath { get; set; }
	[Export] public NodePath NetworkLabelPath { get; set; }
	[Export] public NodePath ScoreLabelPath { get; set; }
	[Export] public NodePath ScanLogLabelPath { get; set; }
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
	private Label _dataLabel;
	private Label _scannerLabel;
	private Label _networkLabel;
	private Label _scoreLabel;
	private Label _scanLogLabel;
	private Label _statusLabel;
	private Control _tutorialOverlay;
	private Button _startButton;
	private Control _resultOverlay;
	private Label _resultTitle;
	private Label _resultSummary;
	private Button _retryButton;
	private Button _resultBackButton;
	private Button _backButton;

	private readonly List<AccessPointState> _accessPoints = new();
	private readonly List<SnifferAgent> _sniffers = new();
	private readonly RandomNumberGenerator _rng = new();
	private Vector2I _playerPosition;
	private Vector2 _playerRenderPosition;
	private bool _running;
	private bool _finished;
	private bool _hasVpn;
	private bool _connected;
	private int _connectedIndex;
	private int _scannerCharges;
	private int _scannedCount;
	private int _dataIntegrity;
	private int _score;
	private int _wrongConnections;
	private int _snifferHits;
	private int _alertLevel;
	private float _timeRemaining;
	private float _moveCooldown;
	private float _snifferCooldown;
	private float _invulnerabilityRemaining;
	private float _statusRemaining;
	private float _pulse;
	private int _lastDisplayedSecond = -1;

	public override void _Ready()
	{
		AudioManager.Instance?.SetGameplayContext("fair_wifi_ghost");
		_rng.Randomize();
		BindUi();

		if (!HasRequiredNodes())
		{
			GD.PrintErr("WifiGhost: estrutura da interface não encontrada.");
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

		foreach (SnifferAgent sniffer in _sniffers)
		{
			sniffer.RenderPosition = sniffer.RenderPosition.MoveToward(
				GridToCenter(sniffer.Position),
				390.0f * step
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
			_snifferCooldown -= step;
			_invulnerabilityRemaining = Math.Max(0.0f, _invulnerabilityRemaining - step);
			HandleMovement();

			if (_snifferCooldown <= 0.0f)
			{
				_snifferCooldown = GetSnifferInterval();
				MoveSniffers();
				CheckSnifferCollision();
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

		if (@event.IsActionPressed("interact"))
			ScanNearestAccessPoint();
		else if (IsKey(keyEvent, Key.C))
			ConnectToNearestAccessPoint();
		else
			return;

		GetViewport().SetInputAsHandled();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor);
		DrawBoard();
		DrawWalls();
		DrawTerminal();

		for (int i = 0; i < _accessPoints.Count; i++)
			DrawAccessPoint(i, _accessPoints[i]);

		if (!_hasVpn)
			DrawVpn();

		DrawSniffers();
		DrawPlayer();
	}

	private static bool IsKey(InputEventKey keyEvent, Key key)
	{
		return keyEvent.Keycode == key || keyEvent.PhysicalKeycode == key;
	}

	private void BindUi()
	{
		_timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
		_dataLabel = GetNodeOrNull<Label>(DataLabelPath);
		_scannerLabel = GetNodeOrNull<Label>(ScannerLabelPath);
		_networkLabel = GetNodeOrNull<Label>(NetworkLabelPath);
		_scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
		_scanLogLabel = GetNodeOrNull<Label>(ScanLogLabelPath);
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
		return _timeLabel != null && _dataLabel != null && _scannerLabel != null &&
			_networkLabel != null && _scoreLabel != null && _scanLogLabel != null &&
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
		_hasVpn = false;
		_connected = false;
		_connectedIndex = -1;
		_scannerCharges = 4;
		_scannedCount = 0;
		_dataIntegrity = 100;
		_score = 0;
		_wrongConnections = 0;
		_snifferHits = 0;
		_alertLevel = 0;
		_timeRemaining = RoundDurationSeconds;
		_moveCooldown = 0.0f;
		_snifferCooldown = BaseSnifferInterval;
		_invulnerabilityRemaining = 0.0f;
		_statusRemaining = 0.0f;
		_lastDisplayedSecond = -1;

		BuildAccessPoints();
		BuildSniffers();
		_tutorialOverlay.Visible = showTutorial;
		_resultOverlay.Visible = false;
		_scanLogLabel.Text = "Nenhuma rede investigada.";
		_statusLabel.Text = "[WASD / SETAS] Aproxime-se de um roteador numerado.";
		UpdateHud(force: true);

		if (showTutorial)
			_startButton.GrabFocus();
	}

	private void BuildAccessPoints()
	{
		var profiles = new List<(string Ssid, string Evidence, bool Legitimate)>
		{
			("Cafe_Clientes", "WPA3 • cadastro do caixa CONFERE", true),
			("Cafe_Freee", "ABERTA • nome imita a cafeteria", false),
			("FREE_WIFI_5G", "ABERTA • solicita senha da escola", false),
			("Cafe_Clientes", "WPA2 • BSSID NÃO confere com o caixa", false)
		};

		for (int i = profiles.Count - 1; i > 0; i--)
		{
			int swapIndex = _rng.RandiRange(0, i);
			var temporary = profiles[i];
			profiles[i] = profiles[swapIndex];
			profiles[swapIndex] = temporary;
		}

		_accessPoints.Clear();
		for (int i = 0; i < AccessPointPositions.Length; i++)
		{
			_accessPoints.Add(new AccessPointState
			{
				Position = AccessPointPositions[i],
				Ssid = profiles[i].Ssid,
				Evidence = profiles[i].Evidence,
				Legitimate = profiles[i].Legitimate,
				Scanned = false,
				Rejected = false
			});
		}
	}

	private void BuildSniffers()
	{
		_sniffers.Clear();
		_sniffers.Add(new SnifferAgent(new Vector2I(4, 3), Vector2I.Right));
		_sniffers.Add(new SnifferAgent(new Vector2I(12, 8), Vector2I.Left));
		_sniffers.Add(new SnifferAgent(new Vector2I(6, 5), Vector2I.Down));
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
			GD.PrintErr($"WifiGhost: falha ao voltar ao Modo Feira: {error}.");
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
		if (!_hasVpn && _playerPosition == VpnPosition)
		{
			_hasVpn = true;
			_score += 250;
			AudioManager.Instance?.PlaySuccess();
			ShowTemporaryStatus("VPN ATIVA — o próximo sniffer será bloqueado.", 1.8f);
		}

		if (_connected && _playerPosition == TerminalPosition)
			FinishRound(true, "Dados entregues por uma conexão segura");

		CheckSnifferCollision();
	}

	private void ScanNearestAccessPoint()
	{
		int index = FindNearestAccessPoint();
		if (index < 0)
		{
			ShowTemporaryStatus("Nenhum roteador ao alcance. Aproxime-se de um dos pontos numerados.", 1.5f);
			return;
		}

		AccessPointState accessPoint = _accessPoints[index];
		if (accessPoint.Scanned)
		{
			ShowTemporaryStatus($"PONTO {index + 1}: {accessPoint.Ssid} — {accessPoint.Evidence}", 2.4f);
			return;
		}

		if (_scannerCharges <= 0)
		{
			ShowTemporaryStatus("Scanner sem cargas. Use as evidências já coletadas.", 1.8f);
			return;
		}

		accessPoint.Scanned = true;
		_scannerCharges--;
		_scannedCount++;
		_score += 180;
		AudioManager.Instance?.PlayInteraction();
		UpdateScanLog();
		ShowTemporaryStatus(
			$"PONTO {index + 1}: {accessPoint.Ssid} — {accessPoint.Evidence}",
			2.6f
		);
		UpdateHud(force: true);
	}

	private void ConnectToNearestAccessPoint()
	{
		if (_connected)
		{
			ShowTemporaryStatus("Conexão segura estabelecida. Leve os dados ao terminal verde.", 1.5f);
			return;
		}

		int index = FindNearestAccessPoint();
		if (index < 0)
		{
			ShowTemporaryStatus("Aproxime-se do roteador escolhido antes de conectar.", 1.5f);
			return;
		}

		AccessPointState accessPoint = _accessPoints[index];
		if (!accessPoint.Scanned)
		{
			ShowTemporaryStatus("Rede não investigada. Pressione [E] antes de conectar.", 1.8f);
			return;
		}

		if (_scannedCount < 2)
		{
			ShowTemporaryStatus("Compare pelo menos duas redes antes de decidir.", 1.8f);
			return;
		}

		if (accessPoint.Legitimate)
		{
			_connected = true;
			_connectedIndex = index;
			_alertLevel++;
			_score += 1400;
			AudioManager.Instance?.PlaySuccess();
			ShowTemporaryStatus("CONEXÃO SEGURA! Agora alcance o terminal verde sem ser interceptado.", 2.4f);
		}
		else
		{
			accessPoint.Rejected = true;
			_wrongConnections++;
			_alertLevel++;
			_dataIntegrity = Math.Max(0, _dataIntegrity - 30);
			_score = Math.Max(0, _score - 250);
			AudioManager.Instance?.PlayError();
			ShowTemporaryStatus("EVIL TWIN! A rede roubou 30% dos dados. Desconectado automaticamente.", 2.6f);
			if (_dataIntegrity <= 0)
				FinishRound(false, "Os dados foram roubados por redes falsas");
		}

		UpdateScanLog();
		UpdateHud(force: true);
	}

	private int FindNearestAccessPoint()
	{
		for (int i = 0; i < _accessPoints.Count; i++)
		{
			Vector2I delta = _accessPoints[i].Position - _playerPosition;
			if (Math.Abs(delta.X) + Math.Abs(delta.Y) <= 1)
				return i;
		}
		return -1;
	}

	private float GetSnifferInterval()
	{
		float interval = BaseSnifferInterval - _alertLevel * 0.055f;
		if (_connected)
			interval -= 0.045f;
		return Math.Max(0.23f, interval);
	}

	private void MoveSniffers()
	{
		foreach (SnifferAgent sniffer in _sniffers)
		{
			int distance = ManhattanDistance(sniffer.Position, _playerPosition);
			bool chase = _connected || distance <= 5 + _alertLevel;
			Vector2I direction = chase
				? ChooseChaseDirection(sniffer.Position)
				: sniffer.Direction;

			Vector2I next = sniffer.Position + direction;
			if (IsWall(next))
			{
				direction = new Vector2I(-direction.Y, direction.X);
				next = sniffer.Position + direction;
				if (IsWall(next))
				{
					direction = -sniffer.Direction;
					next = sniffer.Position + direction;
				}
			}

			if (!IsWall(next))
			{
				sniffer.Position = next;
				sniffer.Direction = direction;
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

	private void CheckSnifferCollision()
	{
		if (_invulnerabilityRemaining > 0.0f || !_running)
			return;

		foreach (SnifferAgent sniffer in _sniffers)
		{
			if (sniffer.Position != _playerPosition)
				continue;

			_invulnerabilityRemaining = InvulnerabilitySeconds;
			if (_hasVpn)
			{
				_hasVpn = false;
				AudioManager.Instance?.PlaySuccess();
				ShowTemporaryStatus("VPN bloqueou a interceptação, mas a proteção foi consumida.", 1.8f);
			}
			else
			{
				_dataIntegrity = Math.Max(0, _dataIntegrity - 20);
				_timeRemaining = Math.Max(0.0f, _timeRemaining - 3.0f);
				_snifferHits++;
				_score = Math.Max(0, _score - 100);
				AudioManager.Instance?.PlayError();
				ShowTemporaryStatus("SNIFFER interceptou 20% dos dados e custou 3 segundos!", 2.0f);
				if (_dataIntegrity <= 0)
					FinishRound(false, "Os sniffers capturaram todos os dados");
			}
			UpdateHud(force: true);
			break;
		}
	}

	private void UpdateScanLog()
	{
		var lines = new List<string>();
		for (int i = 0; i < _accessPoints.Count; i++)
		{
			AccessPointState accessPoint = _accessPoints[i];
			if (!accessPoint.Scanned)
				continue;
			string state = accessPoint.Rejected ? " [FALSA]" : "";
			lines.Add($"P{i + 1}: {accessPoint.Ssid}{state}\n{accessPoint.Evidence}");
		}
		_scanLogLabel.Text = lines.Count == 0
			? "Nenhuma rede investigada."
			: string.Join("\n\n", lines);
	}

	private string GetDefaultStatus()
	{
		if (_connected)
			return "[OBJETIVO] CONEXÃO SEGURA — corra até o TERMINAL VERDE.";

		int index = FindNearestAccessPoint();
		if (index >= 0)
		{
			AccessPointState accessPoint = _accessPoints[index];
			if (!accessPoint.Scanned)
				return $"PONTO {index + 1} AO ALCANCE — aperte [E] para ESCANEAR.";
			if (_scannedCount < 2)
				return $"PONTO {index + 1} ESCANEADO — encontre outra rede e aperte [E].";
			return $"PONTO {index + 1}: {accessPoint.Ssid} — aperte [C] para CONECTAR ou procure outra rede.";
		}

		if (_scannedCount < 2)
			return "[WASD / SETAS] Vá até um roteador. Perto dele, aperte [E].";
		return "[DECIDA] Vá até a rede escolhida e aperte [C] para CONECTAR.";
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
			_score += Mathf.RoundToInt(_timeRemaining * 22.0f) + _dataIntegrity * 12;
			AudioManager.Instance?.PlaySuccess();
		}
		else
		{
			AudioManager.Instance?.PlayError();
		}
		FairModeProgress.RecordResult(
			"wifi_ghost",
			"Operação Wi-Fi Fantasma",
			success,
			_score
		);

		_resultTitle.Text = success ? "CONEXÃO SEGURA" : "DADOS COMPROMETIDOS";
		string lesson = success
			? "Você verificou a proteção e a identidade do roteador antes de conectar."
			: "Nome parecido e sinal forte não provam que uma rede é legítima. Verifique a origem antes de conectar.";
		_resultSummary.Text =
			$"{reason}\n\n" +
			$"Pontuação: {_score}\n" +
			$"Dados preservados: {_dataIntegrity}%\n" +
			$"Redes investigadas: {_scannedCount}/4\n" +
			$"Evil Twins acessadas: {_wrongConnections}\n" +
			$"Interceptações: {_snifferHits}\n" +
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

		_dataLabel.Text = $"Dados  {_dataIntegrity}%";
		_dataLabel.Modulate = _dataIntegrity > 60 ? SuccessColor :
			_dataIntegrity > 30 ? NeutralApColor : DangerColor;
		_scannerLabel.Text = $"Scanner  {_scannerCharges}/4 cargas";
		_networkLabel.Text = _connected
			? $"Conectado  P{_connectedIndex + 1} • SEGURO"
			: "Conectado  NENHUMA REDE";
		_networkLabel.Modulate = _connected ? SuccessColor : ScannedApColor;
		_scoreLabel.Text = $"Pontos  {_score}";
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

	private void DrawAccessPoint(int index, AccessPointState accessPoint)
	{
		Vector2 center = GridToCenter(accessPoint.Position);
		Color color = accessPoint.Rejected ? DangerColor :
			_connectedIndex == index ? SuccessColor :
			accessPoint.Scanned ? ScannedApColor : NeutralApColor;
		float pulse = 3.0f + Mathf.Sin(_pulse * 4.0f + index) * 1.5f;
		DrawCircle(center, 21.0f + pulse, new Color(color.R, color.G, color.B, 0.12f));
		DrawRect(new Rect2(center + new Vector2(-14, -9), new Vector2(28, 18)), new Color(0.03f, 0.07f, 0.11f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-14, -9), new Vector2(28, 18)), color, false, 2.0f);
		DrawLine(center + new Vector2(0, -9), center + new Vector2(0, -18), color, 2.0f);
		for (int dot = 0; dot <= index; dot++)
			DrawCircle(center + new Vector2(-9 + dot * 6, 3), 2.0f, color);
	}

	private void DrawVpn()
	{
		Vector2 center = GridToCenter(VpnPosition);
		DrawCircle(center, 21.0f, new Color(VpnColor.R, VpnColor.G, VpnColor.B, 0.13f));
		DrawCircle(center, 15.0f, new Color(0.08f, 0.04f, 0.14f, 1.0f));
		DrawCircle(center, 15.0f, VpnColor, false, 2.0f);
		DrawLine(center + new Vector2(-7, 0), center + new Vector2(-1, 7), VpnColor, 3.0f);
		DrawLine(center + new Vector2(-1, 7), center + new Vector2(9, -7), VpnColor, 3.0f);
	}

	private void DrawTerminal()
	{
		Vector2 center = GridToCenter(TerminalPosition);
		Color color = _connected ? SuccessColor : new Color(0.36f, 0.46f, 0.5f, 1.0f);
		DrawCircle(center, 24.0f, new Color(color.R, color.G, color.B, 0.12f));
		DrawRect(new Rect2(center + new Vector2(-17, -14), new Vector2(34, 24)), new Color(0.025f, 0.09f, 0.08f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-17, -14), new Vector2(34, 24)), color, false, 2.0f);
		DrawLine(center + new Vector2(0, 10), center + new Vector2(0, 17), color, 3.0f);
		DrawLine(center + new Vector2(-9, 17), center + new Vector2(9, 17), color, 3.0f);
	}

	private void DrawSniffers()
	{
		foreach (SnifferAgent sniffer in _sniffers)
		{
			Vector2 center = sniffer.RenderPosition;
			DrawCircle(center, 19.0f, new Color(DangerColor.R, DangerColor.G, DangerColor.B, 0.13f));
			DrawCircle(center, 13.0f, new Color(0.16f, 0.02f, 0.04f, 1.0f));
			DrawCircle(center, 13.0f, DangerColor, false, 2.0f);
			DrawCircle(center + new Vector2(-4, -2), 2.5f, DangerColor);
			DrawCircle(center + new Vector2(4, -2), 2.5f, DangerColor);
			DrawLine(center + new Vector2(-6, 6), center + new Vector2(6, 6), DangerColor, 2.0f);
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
		if (_hasVpn)
			DrawCircle(center, 20.0f, VpnColor, false, 2.0f);
	}
}
