using Godot;
using System;
using System.Collections.Generic;

public partial class FairModeMenu : Control
{
	[Export] public string MainMenuScenePath { get; set; } = "res://Scenes/Interfaces/main_menu.tscn";
	[Export] public string NetworkMazeScenePath { get; set; } = "res://Scenes/Minigames/network_maze.tscn";
	[Export] public string PhishingHuntScenePath { get; set; } = "res://Scenes/Minigames/phishing_hunt.tscn";
	[Export] public string ServerDefenseScenePath { get; set; } = "res://Scenes/Minigames/server_defense.tscn";
	[Export] public string RansomwareEscapeScenePath { get; set; } = "res://Scenes/Minigames/ransomware_escape.tscn";
	[Export] public string WifiGhostScenePath { get; set; } = "res://Scenes/Minigames/wifi_ghost.tscn";
	[Export] public string DataCenterRescueScenePath { get; set; } = "res://Scenes/Minigames/data_center_rescue.tscn";
	[Export] public NodePath NetworkMazeButtonPath { get; set; }
	[Export] public NodePath PhishingButtonPath { get; set; }
	[Export] public NodePath ServerDefenseButtonPath { get; set; }
	[Export] public NodePath RansomwareEscapeButtonPath { get; set; }
	[Export] public NodePath WifiGhostButtonPath { get; set; }
	[Export] public NodePath DataCenterButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }
	[Export] public NodePath StatusLabelPath { get; set; }
	[Export] public NodePath CurrentPlayerLabelPath { get; set; }
	[Export] public NodePath CurrentStatsLabelPath { get; set; }
	[Export] public NodePath NewParticipantButtonPath { get; set; }
	[Export] public NodePath RankingButtonPath { get; set; }
	[Export] public NodePath ResetDataButtonPath { get; set; }
	[Export] public NodePath RegistrationOverlayPath { get; set; }
	[Export] public NodePath NameInputPath { get; set; }
	[Export] public NodePath StartParticipantButtonPath { get; set; }
	[Export] public NodePath RegistrationCancelButtonPath { get; set; }
	[Export] public NodePath RegistrationErrorLabelPath { get; set; }
	[Export] public NodePath RankingOverlayPath { get; set; }
	[Export] public NodePath RankingTextPath { get; set; }
	[Export] public NodePath CloseRankingButtonPath { get; set; }
	[Export] public NodePath ResetOverlayPath { get; set; }
	[Export] public NodePath ConfirmResetButtonPath { get; set; }
	[Export] public NodePath CancelResetButtonPath { get; set; }

	private Button _networkMazeButton;
	private Button _phishingButton;
	private Button _serverDefenseButton;
	private Button _ransomwareEscapeButton;
	private Button _wifiGhostButton;
	private Button _dataCenterButton;
	private Button _backButton;
	private Label _statusLabel;
	private Label _currentPlayerLabel;
	private Label _currentStatsLabel;
	private Button _newParticipantButton;
	private Button _rankingButton;
	private Button _resetDataButton;
	private Control _registrationOverlay;
	private LineEdit _nameInput;
	private Button _startParticipantButton;
	private Button _registrationCancelButton;
	private Label _registrationErrorLabel;
	private Control _rankingOverlay;
	private Label _rankingText;
	private Button _closeRankingButton;
	private Control _resetOverlay;
	private Button _confirmResetButton;
	private Button _cancelResetButton;

	public override void _Ready()
	{
		AudioManager.Instance?.SetMenuContext();
		CacheNodes();
		if (!HasRequiredNodes())
		{
			GD.PrintErr("FairModeMenu: estrutura da interface não encontrada.");
			return;
		}

		ConnectSignals();
		_registrationOverlay.Visible = false;
		_rankingOverlay.Visible = false;
		_resetOverlay.Visible = false;
		_statusLabel.Visible = false;
		UpdateParticipantSummary();

		if (FairModeProgress.HasActiveParticipant())
			_networkMazeButton.GrabFocus();
		else
			ShowRegistration();
	}

	public override void _ExitTree()
	{
		DisconnectSignals();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel") || @event.IsEcho())
			return;

		if (_resetOverlay.Visible)
			HideResetConfirmation();
		else if (_rankingOverlay.Visible)
			HideRanking();
		else if (_registrationOverlay.Visible && FairModeProgress.HasActiveParticipant())
			HideRegistration();
		else
			OpenScene(MainMenuScenePath, requireParticipant: false);

		GetViewport().SetInputAsHandled();
	}

	private void CacheNodes()
	{
		_networkMazeButton = GetNodeOrNull<Button>(NetworkMazeButtonPath);
		_phishingButton = GetNodeOrNull<Button>(PhishingButtonPath);
		_serverDefenseButton = GetNodeOrNull<Button>(ServerDefenseButtonPath);
		_ransomwareEscapeButton = GetNodeOrNull<Button>(RansomwareEscapeButtonPath);
		_wifiGhostButton = GetNodeOrNull<Button>(WifiGhostButtonPath);
		_dataCenterButton = GetNodeOrNull<Button>(DataCenterButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
		_currentPlayerLabel = GetNodeOrNull<Label>(CurrentPlayerLabelPath);
		_currentStatsLabel = GetNodeOrNull<Label>(CurrentStatsLabelPath);
		_newParticipantButton = GetNodeOrNull<Button>(NewParticipantButtonPath);
		_rankingButton = GetNodeOrNull<Button>(RankingButtonPath);
		_resetDataButton = GetNodeOrNull<Button>(ResetDataButtonPath);
		_registrationOverlay = GetNodeOrNull<Control>(RegistrationOverlayPath);
		_nameInput = GetNodeOrNull<LineEdit>(NameInputPath);
		_startParticipantButton = GetNodeOrNull<Button>(StartParticipantButtonPath);
		_registrationCancelButton = GetNodeOrNull<Button>(RegistrationCancelButtonPath);
		_registrationErrorLabel = GetNodeOrNull<Label>(RegistrationErrorLabelPath);
		_rankingOverlay = GetNodeOrNull<Control>(RankingOverlayPath);
		_rankingText = GetNodeOrNull<Label>(RankingTextPath);
		_closeRankingButton = GetNodeOrNull<Button>(CloseRankingButtonPath);
		_resetOverlay = GetNodeOrNull<Control>(ResetOverlayPath);
		_confirmResetButton = GetNodeOrNull<Button>(ConfirmResetButtonPath);
		_cancelResetButton = GetNodeOrNull<Button>(CancelResetButtonPath);
	}

	private bool HasRequiredNodes()
	{
		return _networkMazeButton != null && _phishingButton != null &&
			_serverDefenseButton != null && _ransomwareEscapeButton != null &&
			_wifiGhostButton != null && _dataCenterButton != null && _backButton != null &&
			_statusLabel != null && _currentPlayerLabel != null && _currentStatsLabel != null &&
			_newParticipantButton != null && _rankingButton != null && _resetDataButton != null &&
			_registrationOverlay != null && _nameInput != null && _startParticipantButton != null &&
			_registrationCancelButton != null && _registrationErrorLabel != null &&
			_rankingOverlay != null && _rankingText != null && _closeRankingButton != null &&
			_resetOverlay != null && _confirmResetButton != null && _cancelResetButton != null;
	}

	private void ConnectSignals()
	{
		_networkMazeButton.Pressed += OnNetworkMazePressed;
		_phishingButton.Pressed += OnPhishingPressed;
		_serverDefenseButton.Pressed += OnServerDefensePressed;
		_ransomwareEscapeButton.Pressed += OnRansomwareEscapePressed;
		_wifiGhostButton.Pressed += OnWifiGhostPressed;
		_dataCenterButton.Pressed += OnDataCenterPressed;
		_backButton.Pressed += OnBackPressed;
		_newParticipantButton.Pressed += ShowRegistration;
		_rankingButton.Pressed += ShowRanking;
		_resetDataButton.Pressed += ShowResetConfirmation;
		_startParticipantButton.Pressed += RegisterParticipant;
		_registrationCancelButton.Pressed += CancelRegistration;
		_nameInput.TextSubmitted += OnNameSubmitted;
		_closeRankingButton.Pressed += HideRanking;
		_confirmResetButton.Pressed += ConfirmReset;
		_cancelResetButton.Pressed += HideResetConfirmation;
	}

	private void DisconnectSignals()
	{
		if (_networkMazeButton != null) _networkMazeButton.Pressed -= OnNetworkMazePressed;
		if (_phishingButton != null) _phishingButton.Pressed -= OnPhishingPressed;
		if (_serverDefenseButton != null) _serverDefenseButton.Pressed -= OnServerDefensePressed;
		if (_ransomwareEscapeButton != null) _ransomwareEscapeButton.Pressed -= OnRansomwareEscapePressed;
		if (_wifiGhostButton != null) _wifiGhostButton.Pressed -= OnWifiGhostPressed;
		if (_dataCenterButton != null) _dataCenterButton.Pressed -= OnDataCenterPressed;
		if (_backButton != null) _backButton.Pressed -= OnBackPressed;
		if (_newParticipantButton != null) _newParticipantButton.Pressed -= ShowRegistration;
		if (_rankingButton != null) _rankingButton.Pressed -= ShowRanking;
		if (_resetDataButton != null) _resetDataButton.Pressed -= ShowResetConfirmation;
		if (_startParticipantButton != null) _startParticipantButton.Pressed -= RegisterParticipant;
		if (_registrationCancelButton != null) _registrationCancelButton.Pressed -= CancelRegistration;
		if (_nameInput != null) _nameInput.TextSubmitted -= OnNameSubmitted;
		if (_closeRankingButton != null) _closeRankingButton.Pressed -= HideRanking;
		if (_confirmResetButton != null) _confirmResetButton.Pressed -= ConfirmReset;
		if (_cancelResetButton != null) _cancelResetButton.Pressed -= HideResetConfirmation;
	}

	private void OnNetworkMazePressed() => OpenScene(NetworkMazeScenePath);
	private void OnPhishingPressed() => OpenScene(PhishingHuntScenePath);
	private void OnServerDefensePressed() => OpenScene(ServerDefenseScenePath);
	private void OnRansomwareEscapePressed() => OpenScene(RansomwareEscapeScenePath);
	private void OnWifiGhostPressed() => OpenScene(WifiGhostScenePath);
	private void OnDataCenterPressed() => OpenScene(DataCenterRescueScenePath);
	private void OnBackPressed() => OpenScene(MainMenuScenePath, requireParticipant: false);

	private void ShowRegistration()
	{
		bool hasParticipant = FairModeProgress.HasActiveParticipant();
		_registrationOverlay.Visible = true;
		_registrationCancelButton.Text = hasParticipant ? "CANCELAR" : "VOLTAR AO MENU";
		_registrationErrorLabel.Visible = false;
		_nameInput.Text = "";
		_nameInput.Editable = true;
		_nameInput.GrabFocus();
	}

	private void HideRegistration()
	{
		_registrationOverlay.Visible = false;
		_networkMazeButton.GrabFocus();
	}

	private void CancelRegistration()
	{
		if (FairModeProgress.HasActiveParticipant())
			HideRegistration();
		else
			OpenScene(MainMenuScenePath, requireParticipant: false);
	}

	private void OnNameSubmitted(string submittedName)
	{
		RegisterParticipant();
	}

	private void RegisterParticipant()
	{
		if (!FairModeProgress.StartNewParticipant(_nameInput.Text, out _))
		{
			_registrationErrorLabel.Text = FairModeProgress.LastError;
			_registrationErrorLabel.Visible = true;
			_nameInput.GrabFocus();
			return;
		}

		_registrationOverlay.Visible = false;
		_statusLabel.Visible = false;
		UpdateParticipantSummary();
		_networkMazeButton.GrabFocus();
		AudioManager.Instance?.PlaySuccess();
	}

	private void UpdateParticipantSummary()
	{
		FairModeProgress.ParticipantRecord participant = FairModeProgress.GetActiveParticipant();
		bool active = participant != null;
		SetChallengeButtonsDisabled(!active);

		if (!active)
		{
			_currentPlayerLabel.Text = "NENHUM PARTICIPANTE ATIVO";
			_currentStatsLabel.Text = "Cadastre um nome para liberar os desafios.";
			return;
		}

		int differentChallenges = participant.Challenges?.Count ?? 0;
		_currentPlayerLabel.Text = $"JOGADOR ATUAL  •  {participant.Name}";
		_currentStatsLabel.Text =
			$"Desafios experimentados  {differentChallenges}/6    •    " +
			$"Partidas  {participant.CompletedRounds}    •    " +
			$"Vitórias  {participant.Victories}    •    " +
			$"Pontos totais  {participant.TotalPoints}";
	}

	private void ShowRanking()
	{
		List<FairModeProgress.ParticipantRecord> ranking = FairModeProgress.GetRanking();
		FairModeProgress.ParticipantRecord active = FairModeProgress.GetActiveParticipant();
		if (ranking.Count == 0)
		{
			_rankingText.Text = "Nenhum participante concluiu uma partida ainda.";
		}
		else
		{
			var lines = new List<string>();
			for (int index = 0; index < ranking.Count; index++)
			{
				FairModeProgress.ParticipantRecord participant = ranking[index];
				string current = participant.SessionId == active?.SessionId ? "  •  ATUAL" : "";
				lines.Add(
					$"{index + 1:00}. {participant.Name}  —  {participant.TotalPoints} pts  •  " +
					$"{participant.Victories} vitórias / {participant.CompletedRounds} partidas{current}"
				);
			}
			_rankingText.Text = string.Join("\n", lines);
		}

		_rankingOverlay.Visible = true;
		_closeRankingButton.GrabFocus();
	}

	private void HideRanking()
	{
		_rankingOverlay.Visible = false;
		_rankingButton.GrabFocus();
	}

	private void ShowResetConfirmation()
	{
		_resetOverlay.Visible = true;
		_cancelResetButton.GrabFocus();
	}

	private void HideResetConfirmation()
	{
		_resetOverlay.Visible = false;
		_resetDataButton.GrabFocus();
	}

	private void ConfirmReset()
	{
		if (!FairModeProgress.ResetAll())
		{
			_resetOverlay.Visible = false;
			ShowStatus("Não foi possível apagar os dados locais.");
			return;
		}

		_resetOverlay.Visible = false;
		UpdateParticipantSummary();
		ShowRegistration();
	}

	private void OpenScene(string scenePath, bool requireParticipant = true)
	{
		if (requireParticipant && !FairModeProgress.HasActiveParticipant())
		{
			ShowRegistration();
			return;
		}

		SetAllButtonsDisabled(true);
		Error error = GetTree().ChangeSceneToFile(scenePath);
		if (error == Error.Ok)
			return;

		SetAllButtonsDisabled(false);
		UpdateParticipantSummary();
		ShowStatus($"Não foi possível abrir a tela: {error}.");
	}

	private void SetChallengeButtonsDisabled(bool disabled)
	{
		_networkMazeButton.Disabled = disabled;
		_phishingButton.Disabled = disabled;
		_serverDefenseButton.Disabled = disabled;
		_ransomwareEscapeButton.Disabled = disabled;
		_wifiGhostButton.Disabled = disabled;
		_dataCenterButton.Disabled = disabled;
	}

	private void SetAllButtonsDisabled(bool disabled)
	{
		SetChallengeButtonsDisabled(disabled);
		_backButton.Disabled = disabled;
		_newParticipantButton.Disabled = disabled;
		_rankingButton.Disabled = disabled;
		_resetDataButton.Disabled = disabled;
	}

	private void ShowStatus(string message)
	{
		_statusLabel.Text = message;
		_statusLabel.Visible = true;
	}
}
