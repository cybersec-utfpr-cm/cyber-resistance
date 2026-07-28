using Godot;

public partial class MainMenu : Control
{
	[Export] public string GameScenePath { get; set; } =
		"res://Scenes/Core/game.tscn";
	[Export] public NodePath NewGameButtonPath { get; set; }
	[Export] public NodePath ContinueButtonPath { get; set; }
	[Export] public NodePath ExitButtonPath { get; set; }
	[Export] public NodePath ConfirmationOverlayPath { get; set; }
	[Export] public NodePath CancelButtonPath { get; set; }
	[Export] public NodePath ConfirmButtonPath { get; set; }
	[Export] public NodePath StatusLabelPath { get; set; }

	private Button _newGameButton;
	private Button _continueButton;
	private Button _exitButton;
	private Control _confirmationOverlay;
	private Button _cancelButton;
	private Button _confirmButton;
	private Label _statusLabel;
	private bool _confirmationOpen;

	public override void _Ready()
	{
		_newGameButton = GetNodeOrNull<Button>(NewGameButtonPath);
		_continueButton = GetNodeOrNull<Button>(ContinueButtonPath);
		_exitButton = GetNodeOrNull<Button>(ExitButtonPath);
		_confirmationOverlay =
			GetNodeOrNull<Control>(ConfirmationOverlayPath);
		_cancelButton = GetNodeOrNull<Button>(CancelButtonPath);
		_confirmButton = GetNodeOrNull<Button>(ConfirmButtonPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);

		if (
			_newGameButton == null ||
			_continueButton == null ||
			_exitButton == null ||
			_confirmationOverlay == null ||
			_cancelButton == null ||
			_confirmButton == null
		)
		{
			GD.PrintErr(
				"MainMenu: estrutura da interface não encontrada."
			);
			return;
		}

		_newGameButton.Pressed += OnNewGamePressed;
		_continueButton.Pressed += OnContinuePressed;
		_exitButton.Pressed += OnExitPressed;
		_cancelButton.Pressed += CloseConfirmation;
		_confirmButton.Pressed += ConfirmNewGame;

		_confirmationOverlay.Visible = false;
		_continueButton.Disabled =
			!(SaveManager.Instance?.HasSaveGame() ?? false);

		if (_statusLabel != null)
			_statusLabel.Visible = false;

		if (!_continueButton.Disabled)
			_continueButton.GrabFocus();
		else
			_newGameButton.GrabFocus();
	}

	public override void _ExitTree()
	{
		if (_newGameButton != null)
			_newGameButton.Pressed -= OnNewGamePressed;

		if (_continueButton != null)
			_continueButton.Pressed -= OnContinuePressed;

		if (_exitButton != null)
			_exitButton.Pressed -= OnExitPressed;

		if (_cancelButton != null)
			_cancelButton.Pressed -= CloseConfirmation;

		if (_confirmButton != null)
			_confirmButton.Pressed -= ConfirmNewGame;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			_confirmationOpen &&
			@event.IsActionPressed("ui_cancel") &&
			!@event.IsEcho()
		)
		{
			CloseConfirmation();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnNewGamePressed()
	{
		if (SaveManager.Instance?.HasSaveGame() ?? false)
		{
			OpenConfirmation();
			return;
		}

		CreateNewGame();
	}

	private void OnContinuePressed()
	{
		if (!(SaveManager.Instance?.HasSaveGame() ?? false))
		{
			_continueButton.Disabled = true;
			ShowStatus("Nenhum jogo salvo foi encontrado.");
			return;
		}

		OpenGameScene();
	}

	private void OnExitPressed()
	{
		GetTree().Quit();
	}

	private void OpenConfirmation()
	{
		_confirmationOpen = true;
		_confirmationOverlay.Visible = true;
		_confirmButton.Disabled = false;
		_confirmButton.Text = "Sim, iniciar novo jogo";
		_cancelButton.Disabled = false;

		if (_statusLabel != null)
			_statusLabel.Visible = false;

		_cancelButton.GrabFocus();
	}

	private void CloseConfirmation()
	{
		if (!_confirmationOpen)
			return;

		_confirmationOpen = false;
		_confirmationOverlay.Visible = false;
		_newGameButton.GrabFocus();
	}

	private void ConfirmNewGame()
	{
		if (!_confirmationOpen)
			return;

		_confirmButton.Disabled = true;
		_confirmButton.Text = "Criando novo jogo...";
		_cancelButton.Disabled = true;
		CreateNewGame();
	}

	private void CreateNewGame()
	{
		bool created =
			SaveManager.Instance?.ResetProgress() ?? false;

		if (created)
		{
			OpenGameScene();
			return;
		}

		_confirmButton.Disabled = false;
		_confirmButton.Text = "Tentar novamente";
		_cancelButton.Disabled = false;
		ShowStatus(
			"Não foi possível criar o novo jogo. " +
			"O progresso anterior foi mantido."
		);
	}

	private void OpenGameScene()
	{
		_newGameButton.Disabled = true;
		_continueButton.Disabled = true;
		_exitButton.Disabled = true;

		Error error = GetTree().ChangeSceneToFile(GameScenePath);

		if (error == Error.Ok)
			return;

		_newGameButton.Disabled = false;
		_continueButton.Disabled =
			!(SaveManager.Instance?.HasSaveGame() ?? false);
		_exitButton.Disabled = false;
		ShowStatus($"Não foi possível abrir o jogo: {error}.");
	}

	private void ShowStatus(string message)
	{
		if (_statusLabel == null)
			return;

		_statusLabel.Text = message;
		_statusLabel.Visible = true;
	}
}
