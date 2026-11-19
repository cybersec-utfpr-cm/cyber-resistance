using Godot;

public partial class PauseManager : Node
{
	private const string PauseMenuScenePath =
		"res://Scenes/Interfaces/pause_menu.tscn";
	private const string MainMenuScenePath =
		"res://Scenes/Interfaces/main_menu.tscn";
	private const string EscapeOverlayGroup = "escape_closes_overlay";

	private PackedScene _pauseMenuScene;
	private PauseMenu _pauseMenu;
	private bool _wasPaused;

	public static PauseManager Instance { get; private set; }
	public bool IsPauseMenuOpen =>
		_pauseMenu != null && GodotObject.IsInstanceValid(_pauseMenu);

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		_pauseMenuScene = GD.Load<PackedScene>(PauseMenuScenePath);
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			!@event.IsActionPressed("ui_cancel") ||
			@event.IsEcho()
		)
		{
			return;
		}

		if (IsPauseMenuOpen)
		{
			ResumeGame();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!IsGameplayActive() || HasEscapeOverlay())
			return;

		if (GetTree().Paused)
			return;

		OpenPauseMenu();
		GetViewport().SetInputAsHandled();
	}

	public void OpenPauseMenu()
	{
		if (IsPauseMenuOpen || !IsGameplayActive())
			return;

		if (_pauseMenuScene == null)
		{
			GD.PrintErr("PauseManager: cena do menu de pausa não encontrada.");
			return;
		}

		_wasPaused = GetTree().Paused;
		_pauseMenu = _pauseMenuScene.Instantiate<PauseMenu>();
		_pauseMenu.TreeExited += OnPauseMenuTreeExited;
		AddChild(_pauseMenu);
		GetTree().Paused = true;
		AudioManager.Instance?.PlayPauseOpen();
	}

	public void ResumeGame()
	{
		if (!IsPauseMenuOpen)
			return;

		AudioManager.Instance?.PlayPauseClose();
		GetTree().Paused = _wasPaused;
		ClosePauseMenu();
	}

	public void ReturnToMainMenu()
	{
		GetTree().Paused = false;
		ClosePauseMenu();
		AudioManager.Instance?.SetMenuContext();

		Error result = GetTree().ChangeSceneToFile(MainMenuScenePath);

		if (result != Error.Ok)
			GD.PrintErr($"PauseManager: falha ao abrir menu: {result}.");
	}

	private void ClosePauseMenu()
	{
		if (!IsPauseMenuOpen)
			return;

		_pauseMenu.Visible = false;
		_pauseMenu.TreeExited -= OnPauseMenuTreeExited;
		_pauseMenu.QueueFree();
		_pauseMenu = null;
	}

	private bool IsGameplayActive()
	{
		return GameManager.Instance != null;
	}

	private bool HasEscapeOverlay()
	{
		foreach (Node node in GetTree().GetNodesInGroup(EscapeOverlayGroup))
		{
			if (
				GodotObject.IsInstanceValid(node) &&
				node.IsInsideTree() &&
				!node.IsQueuedForDeletion()
			)
			{
				return true;
			}
		}

		return false;
	}

	private void OnPauseMenuTreeExited()
	{
		_pauseMenu = null;
	}
}
