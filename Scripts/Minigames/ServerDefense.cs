using System;
using System.Collections.Generic;
using Godot;

public partial class ServerDefense : Control
{
	private const float RoundDurationSeconds = 60.0f;
	private const float ServerZoneHeight = 78.0f;
	private const int MaxIntegrity = 5;
	private const float AntivirusCooldownSeconds = 15.0f;
	private const float RateLimitCooldownSeconds = 14.0f;

	private enum PacketKind
	{
		LegitimateUser,
		LegitimateUpdate,
		Malware,
		BruteForce,
		DdosBot
	}

	private sealed class PacketState
	{
		public PacketKind Kind { get; init; }
		public int Lane { get; init; }
		public float Y { get; set; }
		public float Speed { get; init; }
		public ColorRect Visual { get; init; }
	}

	[Export] public string FairModeScenePath { get; set; } =
		"res://Scenes/Interfaces/fair_mode_menu.tscn";
	[Export] public NodePath TimeLabelPath { get; set; }
	[Export] public NodePath ScoreLabelPath { get; set; }
	[Export] public NodePath IntegrityLabelPath { get; set; }
	[Export] public NodePath ComboLabelPath { get; set; }
	[Export] public NodePath WaveLabelPath { get; set; }
	[Export] public NodePath GameFieldPath { get; set; }
	[Export] public NodePath PacketLayerPath { get; set; }
	[Export] public NodePath FeedbackLabelPath { get; set; }
	[Export] public NodePath Lane1ButtonPath { get; set; }
	[Export] public NodePath Lane2ButtonPath { get; set; }
	[Export] public NodePath Lane3ButtonPath { get; set; }
	[Export] public NodePath AntivirusButtonPath { get; set; }
	[Export] public NodePath RateLimitButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }
	[Export] public NodePath TutorialOverlayPath { get; set; }
	[Export] public NodePath StartButtonPath { get; set; }
	[Export] public NodePath ResultOverlayPath { get; set; }
	[Export] public NodePath ResultTitlePath { get; set; }
	[Export] public NodePath ResultSummaryPath { get; set; }
	[Export] public NodePath RetryButtonPath { get; set; }
	[Export] public NodePath ResultBackButtonPath { get; set; }

	private Label _timeLabel;
	private Label _scoreLabel;
	private Label _integrityLabel;
	private Label _comboLabel;
	private Label _waveLabel;
	private Control _gameField;
	private Control _packetLayer;
	private Label _feedbackLabel;
	private Button _lane1Button;
	private Button _lane2Button;
	private Button _lane3Button;
	private Button _antivirusButton;
	private Button _rateLimitButton;
	private Button _backButton;
	private Control _tutorialOverlay;
	private Button _startButton;
	private Control _resultOverlay;
	private Label _resultTitle;
	private Label _resultSummary;
	private Button _retryButton;
	private Button _resultBackButton;

	private readonly List<PacketState> _packets = new();
	private readonly RandomNumberGenerator _rng = new();
	private bool _running;
	private float _timeRemaining;
	private float _spawnCountdown;
	private float _antivirusCooldown;
	private float _rateLimitCooldown;
	private int _wave;
	private int _integrity;
	private int _score;
	private int _combo;
	private int _maxCombo;
	private int _blockedAttacks;
	private int _legitimateAllowed;
	private int _legitimateBlocked;
	private int _attacksHit;

	private static readonly Color SuccessColor = new(0.42f, 0.94f, 0.72f, 1.0f);
	private static readonly Color DangerColor = new(0.98f, 0.48f, 0.50f, 1.0f);
	private static readonly Color WarningColor = new(0.98f, 0.80f, 0.34f, 1.0f);
	private static readonly Color NeutralColor = new(0.66f, 0.79f, 0.83f, 1.0f);
	private static readonly Color InfoColor = new(0.48f, 0.79f, 1.0f, 1.0f);

	public override void _Ready()
	{
		AudioManager.Instance?.SetMenuContext();
		_rng.Randomize();
		CacheNodes();

		if (!HasRequiredNodes())
		{
			GD.PrintErr("ServerDefense: estrutura da interface não encontrada.");
			return;
		}

		ConnectSignals();
		PrepareRound(showTutorial: true);
	}

	public override void _ExitTree()
	{
		DisconnectSignals();
		ClearPackets();
	}

	public override void _Process(double delta)
	{
		if (!_running)
			return;

		float step = (float)delta;
		_timeRemaining = Math.Max(0.0f, _timeRemaining - step);
		_antivirusCooldown = Math.Max(0.0f, _antivirusCooldown - step);
		_rateLimitCooldown = Math.Max(0.0f, _rateLimitCooldown - step);

		UpdateWave();

		_spawnCountdown -= step;
		while (_spawnCountdown <= 0.0f && _running)
		{
			SpawnPacket();
			_spawnCountdown += GetSpawnInterval();
		}

		UpdatePackets(step);
		UpdateHud();
		UpdateDefenseButtons();

		if (_timeRemaining <= 0.0f && _running)
			FinishRound("SERVIDOR PROTEGIDO");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && !@event.IsEcho())
		{
			OpenFairModeMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_running || @event is not InputEventKey keyEvent ||
			!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (IsKey(keyEvent, (Key)'1'))
			BlockLane(0);
		else if (IsKey(keyEvent, (Key)'2'))
			BlockLane(1);
		else if (IsKey(keyEvent, (Key)'3'))
			BlockLane(2);
		else if (IsKey(keyEvent, Key.A))
			UseAntivirus();
		else if (IsKey(keyEvent, Key.R))
			UseRateLimit();
		else
			return;

		GetViewport().SetInputAsHandled();
	}

	private static bool IsKey(InputEventKey keyEvent, Key key)
	{
		return keyEvent.Keycode == key || keyEvent.PhysicalKeycode == key;
	}

	private void CacheNodes()
	{
		_timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
		_scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
		_integrityLabel = GetNodeOrNull<Label>(IntegrityLabelPath);
		_comboLabel = GetNodeOrNull<Label>(ComboLabelPath);
		_waveLabel = GetNodeOrNull<Label>(WaveLabelPath);
		_gameField = GetNodeOrNull<Control>(GameFieldPath);
		_packetLayer = GetNodeOrNull<Control>(PacketLayerPath);
		_feedbackLabel = GetNodeOrNull<Label>(FeedbackLabelPath);
		_lane1Button = GetNodeOrNull<Button>(Lane1ButtonPath);
		_lane2Button = GetNodeOrNull<Button>(Lane2ButtonPath);
		_lane3Button = GetNodeOrNull<Button>(Lane3ButtonPath);
		_antivirusButton = GetNodeOrNull<Button>(AntivirusButtonPath);
		_rateLimitButton = GetNodeOrNull<Button>(RateLimitButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
		_tutorialOverlay = GetNodeOrNull<Control>(TutorialOverlayPath);
		_startButton = GetNodeOrNull<Button>(StartButtonPath);
		_resultOverlay = GetNodeOrNull<Control>(ResultOverlayPath);
		_resultTitle = GetNodeOrNull<Label>(ResultTitlePath);
		_resultSummary = GetNodeOrNull<Label>(ResultSummaryPath);
		_retryButton = GetNodeOrNull<Button>(RetryButtonPath);
		_resultBackButton = GetNodeOrNull<Button>(ResultBackButtonPath);
	}

	private bool HasRequiredNodes()
	{
		return _timeLabel != null && _scoreLabel != null && _integrityLabel != null &&
			_comboLabel != null && _waveLabel != null && _gameField != null &&
			_packetLayer != null && _feedbackLabel != null && _lane1Button != null &&
			_lane2Button != null && _lane3Button != null && _antivirusButton != null &&
			_rateLimitButton != null && _backButton != null && _tutorialOverlay != null &&
			_startButton != null && _resultOverlay != null && _resultTitle != null &&
			_resultSummary != null && _retryButton != null && _resultBackButton != null;
	}

	private void ConnectSignals()
	{
		_lane1Button.Pressed += OnLane1Pressed;
		_lane2Button.Pressed += OnLane2Pressed;
		_lane3Button.Pressed += OnLane3Pressed;
		_antivirusButton.Pressed += UseAntivirus;
		_rateLimitButton.Pressed += UseRateLimit;
		_backButton.Pressed += OpenFairModeMenu;
		_startButton.Pressed += OnStartPressed;
		_retryButton.Pressed += OnRetryPressed;
		_resultBackButton.Pressed += OpenFairModeMenu;
	}

	private void DisconnectSignals()
	{
		if (_lane1Button != null)
			_lane1Button.Pressed -= OnLane1Pressed;
		if (_lane2Button != null)
			_lane2Button.Pressed -= OnLane2Pressed;
		if (_lane3Button != null)
			_lane3Button.Pressed -= OnLane3Pressed;
		if (_antivirusButton != null)
			_antivirusButton.Pressed -= UseAntivirus;
		if (_rateLimitButton != null)
			_rateLimitButton.Pressed -= UseRateLimit;
		if (_backButton != null)
			_backButton.Pressed -= OpenFairModeMenu;
		if (_startButton != null)
			_startButton.Pressed -= OnStartPressed;
		if (_retryButton != null)
			_retryButton.Pressed -= OnRetryPressed;
		if (_resultBackButton != null)
			_resultBackButton.Pressed -= OpenFairModeMenu;
	}

	private void OnLane1Pressed()
	{
		BlockLane(0);
	}

	private void OnLane2Pressed()
	{
		BlockLane(1);
	}

	private void OnLane3Pressed()
	{
		BlockLane(2);
	}

	private void PrepareRound(bool showTutorial)
	{
		_running = false;
		ClearPackets();
		_timeRemaining = RoundDurationSeconds;
		_spawnCountdown = 0.45f;
		_antivirusCooldown = 0.0f;
		_rateLimitCooldown = 0.0f;
		_wave = 0;
		_integrity = MaxIntegrity;
		_score = 0;
		_combo = 0;
		_maxCombo = 0;
		_blockedAttacks = 0;
		_legitimateAllowed = 0;
		_legitimateBlocked = 0;
		_attacksHit = 0;

		_tutorialOverlay.Visible = showTutorial;
		_resultOverlay.Visible = false;
		ShowFeedback("Observe o tráfego: verde passa, ameaça deve ser bloqueada.", NeutralColor);
		UpdateWave(force: true);
		UpdateHud();
		UpdateDefenseButtons();

		if (showTutorial)
			_startButton.GrabFocus();
	}

	private void OnStartPressed()
	{
		_tutorialOverlay.Visible = false;
		_running = true;
		_spawnCountdown = 0.35f;
		ShowFeedback("DEFESA ATIVA — deixe USUÁRIOS e UPDATES passarem.", SuccessColor);
		UpdateDefenseButtons();
		_lane1Button.GrabFocus();
	}

	private void OnRetryPressed()
	{
		PrepareRound(showTutorial: false);
		_running = true;
		_spawnCountdown = 0.35f;
		ShowFeedback("NOVA RODADA — proteja o servidor por 60 segundos.", InfoColor);
		UpdateDefenseButtons();
		_lane1Button.GrabFocus();
	}

	private void UpdateWave(bool force = false)
	{
		float elapsed = RoundDurationSeconds - _timeRemaining;
		int newWave = elapsed < 20.0f ? 1 : elapsed < 40.0f ? 2 : 3;

		if (!force && newWave == _wave)
			return;

		int previousWave = _wave;
		_wave = newWave;

		switch (_wave)
		{
			case 1:
				_waveLabel.Text = "ONDA 1/3  •  TRÁFEGO NORMAL";
				if (previousWave > 0)
					ShowFeedback("ONDA 1 — identifique malwares sem bloquear usuários.", InfoColor);
				break;
			case 2:
				_waveLabel.Text = "ONDA 2/3  •  TENTATIVAS DE INVASÃO";
				ShowFeedback("ONDA 2 — BRUTE FORCE entrou no tráfego. Atenção às três rotas!", WarningColor);
				if (_running)
					SpawnPacketOfKind(PacketKind.BruteForce, _rng.RandiRange(0, 2));
				break;
			default:
				_waveLabel.Text = "ONDA 3/3  •  ATAQUE DDoS";
				ShowFeedback("ALERTA DDoS — use RATE LIMIT [R] quando os BOTS se acumularem!", DangerColor);
				if (_running)
				{
					SpawnPacketOfKind(PacketKind.DdosBot, 0);
					SpawnPacketOfKind(PacketKind.DdosBot, 1);
					SpawnPacketOfKind(PacketKind.DdosBot, 2);
				}
				break;
		}
	}

	private float GetSpawnInterval()
	{
		return _wave switch
		{
			1 => _rng.RandfRange(1.05f, 1.35f),
			2 => _rng.RandfRange(0.78f, 1.02f),
			_ => _rng.RandfRange(0.52f, 0.76f)
		};
	}

	private void SpawnPacket()
	{
		int lane = _rng.RandiRange(0, 2);
		int roll = _rng.RandiRange(0, 99);
		PacketKind kind;

		if (_wave == 1)
			kind = roll < 60 ? RandomLegitimateKind() : PacketKind.Malware;
		else if (_wave == 2)
			kind = roll < 48 ? RandomLegitimateKind() : roll < 76 ? PacketKind.Malware : PacketKind.BruteForce;
		else
			kind = roll < 34 ? RandomLegitimateKind() : roll < 54 ? PacketKind.Malware :
				roll < 70 ? PacketKind.BruteForce : PacketKind.DdosBot;

		SpawnPacketOfKind(kind, lane);
	}

	private PacketKind RandomLegitimateKind()
	{
		return _rng.RandiRange(0, 1) == 0 ? PacketKind.LegitimateUser : PacketKind.LegitimateUpdate;
	}

	private void SpawnPacketOfKind(PacketKind kind, int lane)
	{
		ColorRect visual = CreatePacketVisual(kind);
		_packetLayer.AddChild(visual);

		float waveMultiplier = _wave switch
		{
			1 => 1.0f,
			2 => 1.13f,
			_ => 1.28f
		};

		PacketState packet = new()
		{
			Kind = kind,
			Lane = lane,
			Y = 42.0f,
			Speed = _rng.RandfRange(84.0f, 104.0f) * waveMultiplier,
			Visual = visual
		};

		_packets.Add(packet);
		PositionPacket(packet);
	}

	private ColorRect CreatePacketVisual(PacketKind kind)
	{
		ColorRect background = new()
		{
			Color = GetPacketColor(kind),
			ZIndex = 0
		};

		Label label = new()
		{
			Text = GetPacketText(kind),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f
		};
		label.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
		label.AddThemeFontSizeOverride("font_size", 12);
		background.AddChild(label);

		return background;
	}

	private static Color GetPacketColor(PacketKind kind)
	{
		return kind switch
		{
			PacketKind.LegitimateUser => new Color(0.07f, 0.38f, 0.25f, 0.98f),
			PacketKind.LegitimateUpdate => new Color(0.06f, 0.30f, 0.42f, 0.98f),
			PacketKind.Malware => new Color(0.52f, 0.08f, 0.10f, 0.98f),
			PacketKind.BruteForce => new Color(0.56f, 0.28f, 0.04f, 0.98f),
			_ => new Color(0.37f, 0.12f, 0.48f, 0.98f)
		};
	}

	private static string GetPacketText(PacketKind kind)
	{
		return kind switch
		{
			PacketKind.LegitimateUser => "[OK]  USUÁRIO",
			PacketKind.LegitimateUpdate => "[OK]  UPDATE",
			PacketKind.Malware => "[!]  MALWARE",
			PacketKind.BruteForce => "[!]  BRUTE FORCE",
			_ => "[!]  BOT DDoS"
		};
	}

	private void UpdatePackets(float delta)
	{
		float serverTop = Math.Max(120.0f, _gameField.Size.Y - ServerZoneHeight);

		for (int index = _packets.Count - 1; index >= 0; index--)
		{
			PacketState packet = _packets[index];
			packet.Y += packet.Speed * delta;
			PositionPacket(packet);

			if (packet.Y + packet.Visual.Size.Y < serverTop)
				continue;

			ResolveArrival(index);
			if (!_running)
				return;
		}
	}

	private void PositionPacket(PacketState packet)
	{
		float laneWidth = Math.Max(120.0f, _gameField.Size.X / 3.0f);
		float packetWidth = Math.Min(178.0f, Math.Max(112.0f, laneWidth - 26.0f));
		float packetHeight = 34.0f;
		float x = packet.Lane * laneWidth + (laneWidth - packetWidth) / 2.0f;

		packet.Visual.Size = new Vector2(packetWidth, packetHeight);
		packet.Visual.Position = new Vector2(x, packet.Y);
	}

	private void ResolveArrival(int index)
	{
		PacketState packet = _packets[index];

		if (IsThreat(packet.Kind))
		{
			_attacksHit++;
			_integrity--;
			_combo = 0;
			_score = Math.Max(0, _score - 90);
			ShowFeedback($"ATAQUE PASSOU! {GetShortName(packet.Kind)} atingiu o servidor. Integridade {_integrity}/{MaxIntegrity}.", DangerColor);
		}
		else
		{
			_legitimateAllowed++;
			_score += 45;
			ShowFeedback($"TRÁFEGO LEGÍTIMO liberado: {GetShortName(packet.Kind)} chegou ao servidor.", SuccessColor);
		}

		RemovePacketAt(index);
		UpdateHud();

		if (_integrity <= 0)
			FinishRound("SERVIDOR COMPROMETIDO");
	}

	private void BlockLane(int lane)
	{
		if (!_running)
			return;

		int targetIndex = -1;
		float closestY = -1.0f;

		for (int index = 0; index < _packets.Count; index++)
		{
			PacketState packet = _packets[index];
			if (packet.Lane != lane || packet.Y <= closestY)
				continue;

			closestY = packet.Y;
			targetIndex = index;
		}

		if (targetIndex < 0)
		{
			ShowFeedback($"ROTA {lane + 1} vazia — espere um pacote antes de bloquear.", NeutralColor);
			return;
		}

		PacketState target = _packets[targetIndex];
		if (IsThreat(target.Kind))
		{
			_blockedAttacks++;
			_combo++;
			_maxCombo = Math.Max(_maxCombo, _combo);
			_score += 140 + Math.Min(100, (_combo - 1) * 20);
			ShowFeedback($"BLOQUEADO! {GetShortName(target.Kind)} parou no firewall da rota {lane + 1}. Combo x{_combo}.", SuccessColor);
		}
		else
		{
			_legitimateBlocked++;
			_combo = 0;
			_score = Math.Max(0, _score - 120);
			ShowFeedback($"FALSO POSITIVO! {GetShortName(target.Kind)} era legítimo. Bloquear tudo também prejudica a rede.", WarningColor);
		}

		RemovePacketAt(targetIndex);
		UpdateHud();
	}

	private void UseAntivirus()
	{
		if (!_running || _antivirusCooldown > 0.0f)
			return;

		int removed = RemovePacketsOfKind(PacketKind.Malware, pointsPerPacket: 90);
		if (removed == 0)
		{
			ShowFeedback("ANTIVÍRUS: nenhum MALWARE detectado agora. Guarde a defesa para o momento certo.", NeutralColor);
			return;
		}

		_antivirusCooldown = AntivirusCooldownSeconds;
		_combo += removed;
		_maxCombo = Math.Max(_maxCombo, _combo);
		ShowFeedback($"ANTIVÍRUS ativado — {removed} malware{(removed == 1 ? "" : "s")} neutralizado{(removed == 1 ? "" : "s")}.", SuccessColor);
		UpdateHud();
		UpdateDefenseButtons();
	}

	private void UseRateLimit()
	{
		if (!_running || _rateLimitCooldown > 0.0f)
			return;

		if (_wave < 3)
		{
			ShowFeedback("RATE LIMIT será necessário na ONDA 3, quando os BOTS DDoS aparecerem.", InfoColor);
			return;
		}

		int removed = RemovePacketsOfKind(PacketKind.DdosBot, pointsPerPacket: 80);
		if (removed == 0)
		{
			ShowFeedback("RATE LIMIT: nenhum BOT DDoS na rede agora. Espere o pico de tráfego.", NeutralColor);
			return;
		}

		_rateLimitCooldown = RateLimitCooldownSeconds;
		_combo += removed;
		_maxCombo = Math.Max(_maxCombo, _combo);
		ShowFeedback($"RATE LIMIT aplicado — {removed} bot{(removed == 1 ? "" : "s")} DDoS descartado{(removed == 1 ? "" : "s")}.", SuccessColor);
		UpdateHud();
		UpdateDefenseButtons();
	}

	private int RemovePacketsOfKind(PacketKind kind, int pointsPerPacket)
	{
		int removed = 0;
		for (int index = _packets.Count - 1; index >= 0; index--)
		{
			if (_packets[index].Kind != kind)
				continue;

			RemovePacketAt(index);
			removed++;
		}

		if (removed > 0)
		{
			_blockedAttacks += removed;
			_score += removed * pointsPerPacket;
		}

		return removed;
	}

	private static bool IsThreat(PacketKind kind)
	{
		return kind is PacketKind.Malware or PacketKind.BruteForce or PacketKind.DdosBot;
	}

	private static string GetShortName(PacketKind kind)
	{
		return kind switch
		{
			PacketKind.LegitimateUser => "USUÁRIO",
			PacketKind.LegitimateUpdate => "UPDATE",
			PacketKind.Malware => "MALWARE",
			PacketKind.BruteForce => "BRUTE FORCE",
			_ => "BOT DDoS"
		};
	}

	private void RemovePacketAt(int index)
	{
		PacketState packet = _packets[index];
		_packets.RemoveAt(index);
		if (GodotObject.IsInstanceValid(packet.Visual))
			packet.Visual.QueueFree();
	}

	private void ClearPackets()
	{
		for (int index = _packets.Count - 1; index >= 0; index--)
		{
			PacketState packet = _packets[index];
			if (GodotObject.IsInstanceValid(packet.Visual))
				packet.Visual.QueueFree();
		}
		_packets.Clear();
	}

	private void UpdateHud()
	{
		int totalSeconds = Math.Max(0, (int)Math.Ceiling(_timeRemaining));
		int minutes = totalSeconds / 60;
		int seconds = totalSeconds % 60;
		_timeLabel.Text = $"TEMPO  {minutes:00}:{seconds:00}";
		_scoreLabel.Text = $"PONTOS  {_score}";
		_integrityLabel.Text = $"INTEGRIDADE  {_integrity}/{MaxIntegrity}";
		_comboLabel.Text = _combo > 1 ? $"COMBO  x{_combo}" : "COMBO  —";

		_integrityLabel.AddThemeColorOverride(
			"font_color",
			_integrity >= 4 ? SuccessColor : _integrity >= 2 ? WarningColor : DangerColor);
	}

	private void UpdateDefenseButtons()
	{
		bool disabled = !_running;
		_lane1Button.Disabled = disabled;
		_lane2Button.Disabled = disabled;
		_lane3Button.Disabled = disabled;

		_antivirusButton.Disabled = disabled || _antivirusCooldown > 0.0f;
		_antivirusButton.Text = _antivirusCooldown > 0.0f
			? $"ANTIVÍRUS [A]  •  RECARGA {Math.Ceiling(_antivirusCooldown):0}s"
			: "ANTIVÍRUS [A]  •  PRONTO";

		_rateLimitButton.Disabled = disabled || _rateLimitCooldown > 0.0f || _wave < 3;
		if (_wave < 3)
			_rateLimitButton.Text = "RATE LIMIT [R]  •  LIBERA NA ONDA 3";
		else if (_rateLimitCooldown > 0.0f)
			_rateLimitButton.Text = $"RATE LIMIT [R]  •  RECARGA {Math.Ceiling(_rateLimitCooldown):0}s";
		else
			_rateLimitButton.Text = "RATE LIMIT [R]  •  PRONTO";
	}

	private void ShowFeedback(string message, Color color)
	{
		_feedbackLabel.Text = message;
		_feedbackLabel.AddThemeColorOverride("font_color", color);
	}

	private void FinishRound(string title)
	{
		if (!_running)
			return;

		_running = false;
		UpdateDefenseButtons();
		ClearPackets();
		_resultTitle.Text = title;

		string lesson;
		if (_integrity <= 0)
			lesson = "Os ataques chegaram ao servidor. Observe a rota e bloqueie apenas as ameaças mais próximas.";
		else if (_legitimateBlocked >= 4)
			lesson = "O servidor resistiu, mas muitos usuários legítimos foram bloqueados. Segurança também precisa preservar disponibilidade.";
		else if (_attacksHit == 0 && _legitimateBlocked <= 1)
			lesson = "Excelente filtragem: ameaças foram contidas sem interromper o tráfego legítimo.";
		else
			lesson = "Boa defesa. Priorize ameaças próximas do servidor e use as ferramentas especiais no momento certo.";

		_resultSummary.Text =
			$"Pontuação: {_score}\n" +
			$"Ataques bloqueados: {_blockedAttacks}\n" +
			$"Tráfego legítimo liberado: {_legitimateAllowed}\n" +
			$"Tráfego legítimo bloqueado: {_legitimateBlocked}\n" +
			$"Ataques que atingiram o servidor: {_attacksHit}\n" +
			$"Maior combo: x{Math.Max(0, _maxCombo)}\n" +
			$"Integridade final: {Math.Max(0, _integrity)}/{MaxIntegrity}\n\n" + lesson;

		_resultOverlay.Visible = true;
		_retryButton.GrabFocus();
	}

	private void OpenFairModeMenu()
	{
		_running = false;
		Error error = GetTree().ChangeSceneToFile(FairModeScenePath);
		if (error != Error.Ok)
			GD.PrintErr($"ServerDefense: não foi possível voltar ao Modo Feira: {error}.");
	}
}
