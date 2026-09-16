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
	[Export] public NodePath NetworkMazeButtonPath { get; set; }
	[Export] public NodePath PhishingButtonPath { get; set; }
	[Export] public NodePath ServerDefenseButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }
	[Export] public NodePath StatusLabelPath { get; set; }

	private Button _networkMazeButton;
	private Button _phishingButton;
	private Button _serverDefenseButton;
	private Button _backButton;
	private Label _statusLabel;

	public override void _Ready()
	{
		AudioManager.Instance?.SetMenuContext();
		_networkMazeButton = GetNodeOrNull<Button>(NetworkMazeButtonPath);
		_phishingButton = GetNodeOrNull<Button>(PhishingButtonPath);
		_serverDefenseButton = GetNodeOrNull<Button>(ServerDefenseButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);

		if (_networkMazeButton == null || _phishingButton == null ||
			_serverDefenseButton == null || _backButton == null)
		{
			GD.PrintErr("FairModeMenu: estrutura da interface não encontrada.");
			return;
		}

		_networkMazeButton.Pressed += OnNetworkMazePressed;
		_phishingButton.Pressed += OnPhishingPressed;
		_serverDefenseButton.Pressed += OnServerDefensePressed;
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
