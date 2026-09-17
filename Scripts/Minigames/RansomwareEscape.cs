using Godot;
using System;

public partial class RansomwareEscape : Control
{
	private const string FairModeMenuScenePath =
		"res://Scenes/Interfaces/fair_mode_menu.tscn";
	private const int Columns = 17;
	private const int Rows = 11;
	private const float CellSize = 48.0f;
	private const float MoveInterval = 0.105f;
	private const float InitialSpreadInterval = 1.50f;
	private const float AlertSpreadInterval = 1.25f;
	private const float CriticalSpreadInterval = 1.05f;
	private const float FinalSpreadInterval = 0.95f;
	private const float AntivirusPauseSeconds = 3.0f;
	private const float DisconnectPauseSeconds = 4.0f;
	private const float RoundDurationSeconds = 75.0f;

	private static readonly string[] Map =
	{
		"#################",
		"#...#...#...#...#",
		"#...#.......#...#",
		"#...#...#...#...#",
		"#.......#...#...#",
		"#...#...#.......#",
		"#...#...#...#...#",
		"#...#...#...#...#",
		"#...#.......#...#",
		"#.......#...#...#",
		"#################"
	};

	private static readonly Vector2 BoardOrigin = new(36.0f, 112.0f);
	private static readonly Vector2I PlayerStart = new(1, 5);
	private static readonly Vector2I[] FilePositions =
	{
		new(3, 2),
		new(7, 7),
		new(11, 3)
	};
	private static readonly Vector2I AntivirusPosition = new(6, 2);
	private static readonly Vector2I DisconnectPosition = new(10, 8);
	private static readonly Vector2I BackupPosition = new(14, 8);
	private static readonly Vector2I RecoveryTerminalPosition = new(15, 2);

	private static readonly Color BackgroundColor = new(0.008f, 0.018f, 0.03f, 1.0f);
	private static readonly Color BoardColor = new(0.025f, 0.045f, 0.065f, 1.0f);
	private static readonly Color GridColor = new(0.08f, 0.16f, 0.2f, 0.5f);
	private static readonly Color WallColor = new(0.055f, 0.12f, 0.15f, 1.0f);
	private static readonly Color WallEdgeColor = new(0.12f, 0.35f, 0.39f, 1.0f);
	private static readonly Color InfectionColor = new(0.8f, 0.07f, 0.12f, 0.46f);
	private static readonly Color InfectionEdgeColor = new(1.0f, 0.2f, 0.24f, 1.0f);
	private static readonly Color PlayerColor = new(0.34f, 0.88f, 0.95f, 1.0f);
	private static readonly Color FileColor = new(0.98f, 0.78f, 0.26f, 1.0f);
	private static readonly Color BackupColor = new(0.36f, 0.66f, 1.0f, 1.0f);
	private static readonly Color SuccessColor = new(0.34f, 0.9f, 0.62f, 1.0f);
	private static readonly Color AntivirusColor = new(0.42f, 0.94f, 0.72f, 1.0f);
	private static readonly Color DisconnectColor = new(0.72f, 0.5f, 0.96f, 1.0f);

	[Export] public NodePath TimeLabelPath { get; set; }
	[Export] public NodePath FilesLabelPath { get; set; }
	[Export] public NodePath BackupLabelPath { get; set; }
	[Export] public NodePath ScoreLabelPath { get; set; }
	[Export] public NodePath SpreadLabelPath { get; set; }
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
	private Label _filesLabel;
	private Label _backupLabel;
	private Label _scoreLabel;
	private Label _spreadLabel;
	private Label _statusLabel;
	private Control _tutorialOverlay;
	private Button _startButton;
	private Control _resultOverlay;
	private Label _resultTitle;
	private Label _resultSummary;
	private Button _retryButton;
	private Button _resultBackButton;
	private Button _backButton;

	private readonly bool[] _collectedFiles = new bool[FilePositions.Length];
	private Vector2I _playerPosition;
	private Vector2 _playerRenderPosition;
	private bool _running;
	private bool _finished;
	private bool _backupCompleted;
	private bool _antivirusUsed;
	private bool _disconnectUsed;
	private int _filesCollected;
	private int _infectedThroughColumn;
	private int _score;
	private float _timeRemaining;
	private float _moveCooldown;
	private float _spreadCountdown;
	private float _spreadPausedRemaining;
	private float _statusRemaining;
	private float _pulse;
	private int _lastDisplayedSecond = -1;

	public override void _Ready()
	{
		AudioManager.Instance?.SetGameplayContext("fair_ransomware_escape");
		BindUi();

		if (!HasRequiredNodes())
		{
			GD.PrintErr("RansomwareEscape: estrutura da interface não encontrada.");
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

		if (_statusRemaining > 0.0f)
		{
			_statusRemaining -= step;
			if (_statusRemaining <= 0.0f && _statusLabel != null)
				_statusLabel.Text = GetDefaultStatus();
		}

		if (_running && !_finished)
		{
			_timeRemaining = Math.Max(0.0f, _timeRemaining - step);
			_moveCooldown -= step;
			HandlePlayerInput();

			if (_spreadPausedRemaining > 0.0f)
			{
				_spreadPausedRemaining = Math.Max(0.0f, _spreadPausedRemaining - step);
			}
			else
			{
				_spreadCountdown -= step;
				while (_spreadCountdown <= 0.0f && _running)
				{
					_spreadCountdown += GetSpreadInterval();
					AdvanceRansomware();
				}
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
		}
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor);
		DrawBoard();
		DrawInfection();
		DrawWalls();

		for (int i = 0; i < FilePositions.Length; i++)
		{
			if (!_collectedFiles[i])
				DrawFile(FilePositions[i]);
		}

		if (!_antivirusUsed)
			DrawAntivirus(AntivirusPosition);
		if (!_disconnectUsed)
			DrawDisconnect(DisconnectPosition);

		DrawBackup(BackupPosition);
		DrawRecoveryTerminal(RecoveryTerminalPosition);
		DrawPlayer();
	}

	private void BindUi()
	{
		_timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
		_filesLabel = GetNodeOrNull<Label>(FilesLabelPath);
		_backupLabel = GetNodeOrNull<Label>(BackupLabelPath);
		_scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
		_spreadLabel = GetNodeOrNull<Label>(SpreadLabelPath);
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
		return _timeLabel != null && _filesLabel != null && _backupLabel != null &&
			_scoreLabel != null && _spreadLabel != null && _statusLabel != null &&
			_tutorialOverlay != null && _startButton != null && _resultOverlay != null &&
			_resultTitle != null && _resultSummary != null && _retryButton != null &&
			_resultBackButton != null && _backButton != null;
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
		_backupCompleted = false;
		_antivirusUsed = false;
		_disconnectUsed = false;
		_filesCollected = 0;
		_infectedThroughColumn = 0;
		_score = 0;
		_timeRemaining = RoundDurationSeconds;
		_moveCooldown = 0.0f;
		_spreadCountdown = GetSpreadInterval();
		_spreadPausedRemaining = 0.0f;
		_statusRemaining = 0.0f;
		_lastDisplayedSecond = -1;

		for (int i = 0; i < _collectedFiles.Length; i++)
			_collectedFiles[i] = false;

		_tutorialOverlay.Visible = showTutorial;
		_resultOverlay.Visible = false;
		_statusLabel.Text = "Recupere os três arquivos antes que a onda vermelha chegue.";
		UpdateHud(force: true);

		if (showTutorial)
			_startButton.GrabFocus();
	}

	private void StartRound()
	{
		_tutorialOverlay.Visible = false;
		_running = true;
		_timeRemaining = RoundDurationSeconds;
		_spreadCountdown = GetSpreadInterval();
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
			GD.PrintErr($"RansomwareEscape: falha ao voltar ao Modo Feira: {error}.");
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
			ShowTemporaryStatus("Caminho bloqueado. Procure outra passagem.", 1.0f);
			return;
		}

		if (next.X <= _infectedThroughColumn)
		{
			FinishRound(false, "Você entrou na área criptografada");
			return;
		}

		_playerPosition = next;
		CheckPickupsAndObjectives();
	}

	private void CheckPickupsAndObjectives()
	{
		for (int i = 0; i < FilePositions.Length; i++)
		{
			if (_collectedFiles[i] || _playerPosition != FilePositions[i])
				continue;

			_collectedFiles[i] = true;
			_filesCollected++;
			_spreadCountdown = Math.Min(_spreadCountdown, GetSpreadInterval());
			_score += 650;
			AudioManager.Instance?.PlayInteraction();
			string pressureWarning = _filesCollected switch
			{
				1 => " O ransomware percebeu o resgate e acelerou!",
				2 => " ALERTA: propagação em velocidade crítica!",
				_ => " Velocidade máxima: faça o backup agora!"
			};
			ShowTemporaryStatus(
				$"ARQUIVO SALVO! {_filesCollected}/3 recuperados.{pressureWarning}",
				2.0f
			);
		}

		if (!_antivirusUsed && _playerPosition == AntivirusPosition)
		{
			_antivirusUsed = true;
			_spreadPausedRemaining = Math.Max(
				_spreadPausedRemaining,
				AntivirusPauseSeconds
			);
			_score += 300;
			AudioManager.Instance?.PlaySuccess();
			ShowTemporaryStatus("ANTIVÍRUS ATIVO — propagação pausada por 3 segundos!", 2.0f);
		}

		if (!_disconnectUsed && _playerPosition == DisconnectPosition)
		{
			_disconnectUsed = true;
			_spreadPausedRemaining = Math.Max(
				_spreadPausedRemaining,
				DisconnectPauseSeconds
			);
			_score += 400;
			AudioManager.Instance?.PlaySuccess();
			ShowTemporaryStatus("REDE DESCONECTADA — você ganhou mais 4 segundos!", 2.0f);
		}

		if (_playerPosition == BackupPosition && !_backupCompleted)
		{
			if (_filesCollected < FilePositions.Length)
			{
				ShowTemporaryStatus("O backup precisa dos 3 arquivos. Continue o resgate!", 1.8f);
			}
			else
			{
				_backupCompleted = true;
				_score += 1000;
				AudioManager.Instance?.PlaySuccess();
				ShowTemporaryStatus("BACKUP CONCLUÍDO! Corra para o terminal verde.", 2.2f);
			}
		}

		if (_playerPosition == RecoveryTerminalPosition)
		{
			if (_backupCompleted)
				FinishRound(true, "Sistema restaurado");
			else
				ShowTemporaryStatus("Terminal bloqueado: salve os arquivos e conclua o backup.", 2.0f);
		}

		UpdateHud(force: true);
	}

	private void AdvanceRansomware()
	{
		_infectedThroughColumn = Math.Min(Columns - 2, _infectedThroughColumn + 1);
		AudioManager.Instance?.PlayError();

		if (_playerPosition.X <= _infectedThroughColumn)
		{
			FinishRound(false, "O ransomware alcançou você");
			return;
		}

		for (int i = 0; i < FilePositions.Length; i++)
		{
			if (!_collectedFiles[i] && FilePositions[i].X <= _infectedThroughColumn)
			{
				FinishRound(false, "Um arquivo importante foi criptografado");
				return;
			}
		}

		if (!_backupCompleted && BackupPosition.X <= _infectedThroughColumn)
		{
			FinishRound(false, "O dispositivo de backup foi criptografado");
			return;
		}

		ShowTemporaryStatus(
			$"ALERTA: ransomware avançou para o setor {_infectedThroughColumn}!",
			0.9f
		);
		UpdateHud(force: true);
	}

	private void FinishRound(bool success, string reason)
	{
		if (_finished)
			return;

		_running = false;
		_finished = true;
		if (success)
		{
			_score += Mathf.RoundToInt(_timeRemaining * 30.0f);
			AudioManager.Instance?.PlaySuccess();
		}
		else
		{
			AudioManager.Instance?.PlayError();
		}
		FairModeProgress.RecordResult(
			"ransomware_escape",
			"Fuga do Ransomware",
			success,
			_score
		);

		_resultTitle.Text = success ? "ARQUIVOS PROTEGIDOS" : "SISTEMA CRIPTOGRAFADO";
		string lesson = success
			? "Você isolou a ameaça e criou uma cópia segura antes da restauração."
			: "Em um incidente real, isole o equipamento e não pague nem improvise: acione o responsável pela segurança.";

		_resultSummary.Text =
			$"{reason}\n\n" +
			$"Pontuação: {_score}\n" +
			$"Arquivos recuperados: {_filesCollected}/3\n" +
			$"Backup concluído: {(_backupCompleted ? "SIM" : "NÃO")}\n" +
			$"Tempo restante: {FormatTime(_timeRemaining)}\n" +
			$"Ferramentas usadas: {GetToolsUsedCount()}/2\n\n" +
			$"LIÇÃO: {lesson}";

		_resultOverlay.Visible = true;
		_retryButton.GrabFocus();
		UpdateHud(force: true);
	}

	private int GetToolsUsedCount()
	{
		return (_antivirusUsed ? 1 : 0) + (_disconnectUsed ? 1 : 0);
	}

	private float GetSpreadInterval()
	{
		return _filesCollected switch
		{
			0 => InitialSpreadInterval,
			1 => AlertSpreadInterval,
			2 => CriticalSpreadInterval,
			_ => FinalSpreadInterval
		};
	}

	private string GetThreatLevel()
	{
		return _filesCollected switch
		{
			0 => "ATIVA",
			1 => "ACELERADA",
			2 => "CRÍTICA",
			_ => "VELOCIDADE MÁXIMA"
		};
	}

	private string GetDefaultStatus()
	{
		if (_filesCollected < FilePositions.Length)
			return $"Recupere os arquivos: {_filesCollected}/3. A onda vermelha continua avançando.";
		if (!_backupCompleted)
			return "Arquivos recuperados. Vá até o dispositivo azul e conclua o backup.";
		return "Backup seguro. Alcance o terminal verde para restaurar o sistema.";
	}

	private void ShowTemporaryStatus(string message, float duration)
	{
		_statusLabel.Text = message;
		_statusRemaining = duration;
	}

	private void UpdateHud(bool force = false)
	{
		int displayedSecond = Mathf.CeilToInt(_timeRemaining);
		if (force || displayedSecond != _lastDisplayedSecond)
		{
			_lastDisplayedSecond = displayedSecond;
			_timeLabel.Text = $"Tempo  {FormatTime(_timeRemaining)}";
		}

		_filesLabel.Text = $"Arquivos  {_filesCollected}/3";
		_backupLabel.Text = _backupCompleted ? "Backup  SEGURO" : "Backup  PENDENTE";
		_backupLabel.Modulate = _backupCompleted ? SuccessColor : FileColor;
		_scoreLabel.Text = $"Pontos  {_score}";

		if (_spreadPausedRemaining > 0.0f)
		{
			_spreadLabel.Text = $"PROPAGAÇÃO PAUSADA  {_spreadPausedRemaining:0.0}s";
			_spreadLabel.Modulate = AntivirusColor;
		}
		else
		{
			_spreadLabel.Text =
				$"RANSOMWARE {GetThreatLevel()}  •  SETOR {_infectedThroughColumn}/{Columns - 2}";
			_spreadLabel.Modulate = InfectionEdgeColor;
		}
	}

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Math.Max(0, Mathf.CeilToInt(seconds));
		return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
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
			DrawLine(
				new Vector2(px, BoardOrigin.Y),
				new Vector2(px, BoardOrigin.Y + Rows * CellSize),
				GridColor,
				1.0f
			);
		}

		for (int y = 1; y < Rows; y++)
		{
			float py = BoardOrigin.Y + y * CellSize;
			DrawLine(
				new Vector2(BoardOrigin.X, py),
				new Vector2(BoardOrigin.X + Columns * CellSize, py),
				GridColor,
				1.0f
			);
		}
	}

	private void DrawInfection()
	{
		for (int y = 1; y < Rows - 1; y++)
		{
			for (int x = 1; x <= _infectedThroughColumn; x++)
			{
				Vector2I cell = new(x, y);
				if (IsWall(cell))
					continue;

				Rect2 rect = CellRect(cell, 2.0f);
				DrawRect(rect, InfectionColor);
				DrawLine(rect.Position + new Vector2(8, 8), rect.End - new Vector2(8, 8), InfectionEdgeColor, 1.5f);
				DrawLine(
					new Vector2(rect.End.X - 8, rect.Position.Y + 8),
					new Vector2(rect.Position.X + 8, rect.End.Y - 8),
					InfectionEdgeColor,
					1.5f
				);
			}
		}

		if (_infectedThroughColumn > 0)
		{
			float edgeX = BoardOrigin.X + (_infectedThroughColumn + 1) * CellSize;
			float width = 3.0f + Mathf.Sin(_pulse * 7.0f) * 1.5f;
			DrawLine(
				new Vector2(edgeX, BoardOrigin.Y + CellSize),
				new Vector2(edgeX, BoardOrigin.Y + (Rows - 1) * CellSize),
				InfectionEdgeColor,
				width
			);
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

	private void DrawFile(Vector2I position)
	{
		Vector2 center = GridToCenter(position);
		float glow = 3.0f + Mathf.Sin(_pulse * 4.0f + position.X) * 1.5f;
		DrawCircle(center, 19.0f + glow, new Color(FileColor.R, FileColor.G, FileColor.B, 0.12f));
		DrawRect(new Rect2(center + new Vector2(-14, -9), new Vector2(28, 22)), new Color(0.15f, 0.12f, 0.03f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-14, -9), new Vector2(28, 22)), FileColor, false, 2.0f);
		DrawRect(new Rect2(center + new Vector2(-10, -14), new Vector2(13, 6)), FileColor);
		DrawLine(center + new Vector2(-8, -2), center + new Vector2(8, -2), FileColor, 2.0f);
		DrawLine(center + new Vector2(-8, 5), center + new Vector2(5, 5), FileColor, 2.0f);
	}

	private void DrawAntivirus(Vector2I position)
	{
		Vector2 center = GridToCenter(position);
		DrawCircle(center, 18.0f, new Color(0.03f, 0.15f, 0.11f, 1.0f));
		DrawCircle(center, 18.0f, AntivirusColor, false, 2.0f);
		DrawLine(center + new Vector2(-8, 0), center + new Vector2(8, 0), AntivirusColor, 4.0f);
		DrawLine(center + new Vector2(0, -8), center + new Vector2(0, 8), AntivirusColor, 4.0f);
	}

	private void DrawDisconnect(Vector2I position)
	{
		Vector2 center = GridToCenter(position);
		DrawCircle(center, 19.0f, new Color(0.1f, 0.05f, 0.17f, 1.0f));
		DrawCircle(center, 19.0f, DisconnectColor, false, 2.0f);
		DrawLine(center + new Vector2(-10, -9), center + new Vector2(10, 9), DisconnectColor, 3.0f);
		DrawLine(center + new Vector2(-10, 9), center + new Vector2(10, -9), DisconnectColor, 3.0f);
	}

	private void DrawBackup(Vector2I position)
	{
		Vector2 center = GridToCenter(position);
		Color color = _backupCompleted ? SuccessColor : BackupColor;
		DrawCircle(center, 22.0f, new Color(color.R, color.G, color.B, 0.13f));
		DrawRect(new Rect2(center + new Vector2(-15, -16), new Vector2(30, 32)), new Color(0.03f, 0.08f, 0.15f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-15, -16), new Vector2(30, 32)), color, false, 2.0f);
		DrawRect(new Rect2(center + new Vector2(-8, -11), new Vector2(16, 10)), color, false, 2.0f);
		DrawCircle(center + new Vector2(0, 8), 3.5f, color);
	}

	private void DrawRecoveryTerminal(Vector2I position)
	{
		Vector2 center = GridToCenter(position);
		Color color = _backupCompleted ? SuccessColor : new Color(0.35f, 0.46f, 0.48f, 1.0f);
		DrawCircle(center, 24.0f, new Color(color.R, color.G, color.B, 0.11f));
		DrawRect(new Rect2(center + new Vector2(-17, -14), new Vector2(34, 24)), new Color(0.025f, 0.09f, 0.08f, 1.0f));
		DrawRect(new Rect2(center + new Vector2(-17, -14), new Vector2(34, 24)), color, false, 2.0f);
		DrawLine(center + new Vector2(0, 10), center + new Vector2(0, 17), color, 3.0f);
		DrawLine(center + new Vector2(-9, 17), center + new Vector2(9, 17), color, 3.0f);
		DrawLine(center + new Vector2(-8, -3), center + new Vector2(-2, 3), color, 2.0f);
		DrawLine(center + new Vector2(-2, 3), center + new Vector2(9, -7), color, 2.0f);
	}

	private void DrawPlayer()
	{
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
