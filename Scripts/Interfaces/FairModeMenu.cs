using Godot;

public partial class FairModeMenu : Control
{
	[Export] public string MainMenuScenePath { get; set; } =
		"res://Scenes/Interfaces/main_menu.tscn";
	[Export] public string NetworkMazeScenePath { get; set; } =
		"res://Scenes/Minigames/network_maze.tscn";
	[Export] public string PhishingHuntScenePath { get; set; } =
		"res://Scenes/Minigames/phishing_hunt.tscn";
	[Export] public string ServerDefenseScenePath { get; set; } =
		"res://Scenes/Minigames/server_defense.tscn";
	[Export] public string RansomwareEscapeScenePath { get; set; } =
		"res://Scenes/Minigames/ransomware_escape.tscn";
	[Export] public string WifiGhostScenePath { get; set; } =
		"res://Scenes/Minigames/wifi_ghost.tscn";
	[Export] public string DataCenterRescueScenePath { get; set; } =
		"res://Scenes/Minigames/data_center_rescue.tscn";
	[Export] public NodePath NetworkMazeButtonPath { get; set; }
	[Export] public NodePath PhishingButtonPath { get; set; }
	[Export] public NodePath ServerDefenseButtonPath { get; set; }
	[Export] public NodePath RansomwareEscapeButtonPath { get; set; }
	[Export] public NodePath WifiGhostButtonPath { get; set; }
	[Export] public NodePath DataCenterButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }
	[Export] public NodePath StatusLabelPath { get; set; }

	private Button _networkMazeButton;
	private Button _phishingButton;
	private Button _serverDefenseButton;
	private Button _ransomwareEscapeButton;
	private Button _wifiGhostButton;
	private Button _dataCenterButton;
	private Button _backButton;
	private Label _statusLabel;

	public override void _Ready()
	{
		AudioManager.Instance?.SetMenuContext();
		_networkMazeButton = GetNodeOrNull<Button>(NetworkMazeButtonPath);
		_phishingButton = GetNodeOrNull<Button>(PhishingButtonPath);
		_serverDefenseButton = GetNodeOrNull<Button>(ServerDefenseButtonPath);
		_ransomwareEscapeButton = GetNodeOrNull<Button>(RansomwareEscapeButtonPath);
		_wifiGhostButton = GetNodeOrNull<Button>(WifiGhostButtonPath);
		_dataCenterButton = GetNodeOrNull<Button>(DataCenterButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);

		if (_networkMazeButton == null || _phishingButton == null ||
			_serverDefenseButton == null || _ransomwareEscapeButton == null ||
			_wifiGhostButton == null || _dataCenterButton == null || _backButton == null)
		{
			GD.PrintErr("FairModeMenu: estrutura da interface não encontrada.");
			return;
		}

		_networkMazeButton.Pressed += OnNetworkMazePressed;
		_phishingButton.Pressed += OnPhishingPressed;
		_serverDefenseButton.Pressed += OnServerDefensePressed;
		_ransomwareEscapeButton.Pressed += OnRansomwareEscapePressed;
		_wifiGhostButton.Pressed += OnWifiGhostPressed;
		_dataCenterButton.Pressed += OnDataCenterPressed;
		_backButton.Pressed += OnBackPressed;

		if (_statusLabel != null)
			_statusLabel.Visible = false;

		_networkMazeButton.GrabFocus();
	}

	public override void _ExitTree()
	{
		if (_networkMazeButton != null)
			_networkMazeButton.Pressed -= OnNetworkMazePressed;

		if (_phishingButton != null)
			_phishingButton.Pressed -= OnPhishingPressed;

		if (_serverDefenseButton != null)
			_serverDefenseButton.Pressed -= OnServerDefensePressed;

		if (_ransomwareEscapeButton != null)
			_ransomwareEscapeButton.Pressed -= OnRansomwareEscapePressed;

		if (_wifiGhostButton != null)
			_wifiGhostButton.Pressed -= OnWifiGhostPressed;

		if (_dataCenterButton != null)
			_dataCenterButton.Pressed -= OnDataCenterPressed;

		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && !@event.IsEcho())
		{
			OpenScene(MainMenuScenePath);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnNetworkMazePressed()
	{
		OpenScene(NetworkMazeScenePath);
	}

	private void OnPhishingPressed()
	{
		OpenScene(PhishingHuntScenePath);
	}

	private void OnServerDefensePressed()
	{
		OpenScene(ServerDefenseScenePath);
	}

	private void OnRansomwareEscapePressed()
	{
		OpenScene(RansomwareEscapeScenePath);
	}

	private void OnWifiGhostPressed()
	{
		OpenScene(WifiGhostScenePath);
	}

	private void OnDataCenterPressed()
	{
		OpenScene(DataCenterRescueScenePath);
	}

	private void OnBackPressed()
	{
		OpenScene(MainMenuScenePath);
	}

	private void OpenScene(string scenePath)
	{
		SetButtonsDisabled(true);

		Error error = GetTree().ChangeSceneToFile(scenePath);

		if (error == Error.Ok)
			return;

		SetButtonsDisabled(false);
		ShowStatus($"Não foi possível abrir a tela: {error}.");
	}

	private void SetButtonsDisabled(bool disabled)
	{
		if (_networkMazeButton != null)
			_networkMazeButton.Disabled = disabled;
		if (_phishingButton != null)
			_phishingButton.Disabled = disabled;
		if (_serverDefenseButton != null)
			_serverDefenseButton.Disabled = disabled;
		if (_ransomwareEscapeButton != null)
			_ransomwareEscapeButton.Disabled = disabled;
		if (_wifiGhostButton != null)
			_wifiGhostButton.Disabled = disabled;
		if (_dataCenterButton != null)
			_dataCenterButton.Disabled = disabled;
		if (_backButton != null)
			_backButton.Disabled = disabled;
	}

	private void ShowStatus(string message)
	{
		if (_statusLabel == null)
			return;

		_statusLabel.Text = message;
		_statusLabel.Visible = true;
	}
}
