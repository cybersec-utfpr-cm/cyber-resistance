using Godot;

public partial class FairModeMenu : Control
{
	[Export] public string MainMenuScenePath { get; set; } =
		"res://Scenes/Interfaces/main_menu.tscn";
	[Export] public string NetworkMazeScenePath { get; set; } =
		"res://Scenes/Minigames/network_maze.tscn";
	[Export] public NodePath NetworkMazeButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }
	[Export] public NodePath StatusLabelPath { get; set; }

	private Button _networkMazeButton;
	private Button _backButton;
	private Label _statusLabel;

	public override void _Ready()
	{
		AudioManager.Instance?.SetMenuContext();
		_networkMazeButton = GetNodeOrNull<Button>(NetworkMazeButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);

		if (_networkMazeButton == null || _backButton == null)
		{
			GD.PrintErr("FairModeMenu: estrutura da interface não encontrada.");
			return;
		}

		_networkMazeButton.Pressed += OnNetworkMazePressed;
		_backButton.Pressed += OnBackPressed;

		if (_statusLabel != null)
			_statusLabel.Visible = false;

		_networkMazeButton.GrabFocus();
	}

	public override void _ExitTree()
	{
		if (_networkMazeButton != null)
			_networkMazeButton.Pressed -= OnNetworkMazePressed;

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

	private void OnBackPressed()
	{
		OpenScene(MainMenuScenePath);
	}

	private void OpenScene(string scenePath)
	{
		_networkMazeButton.Disabled = true;
		_backButton.Disabled = true;

		Error error = GetTree().ChangeSceneToFile(scenePath);

		if (error == Error.Ok)
			return;

		_networkMazeButton.Disabled = false;
		_backButton.Disabled = false;
		ShowStatus($"Não foi possível abrir a tela: {error}.");
	}

	private void ShowStatus(string message)
	{
		if (_statusLabel == null)
			return;

		_statusLabel.Text = message;
		_statusLabel.Visible = true;
	}
}
