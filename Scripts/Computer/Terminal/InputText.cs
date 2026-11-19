using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class InputText : Control
{
	[Export] public string terminalHost = "127.0.0.1";
	[Export] public int terminalPort = 5000;
	[Export] public string terminalUsername = "player";
	[Export] public string terminalPassword = "player";
	[Export] public int terminalLoginTimeoutMs = 1000;
	[Export] public int terminalMaxAttempts = 10;
	[Export] public int terminalRetryDelayMs = 750;
	[Export] public int terminalSlowWarningAttempt = 4;
	[Export] public int terminalTotalTimeoutMs = 15000;

	private TerminalSurface _emulator;
	private TerminalController _terminalController;
	private Terminal _terminal;
	private Control _connectionOverlay;
	private Label _statusLabel;
	private Label _detailLabel;
	private Label _sessionStatus;
	private Button _retryButton;
	private Button _exitButton;
	private readonly CancellationTokenSource _lifetimeCts = new();
	private bool _isPreparingEnvironment;
	private bool _hasRequestedInitialization;
	private bool _inputEnabled;

	public bool HasTerminalFocus =>
		_emulator != null && _emulator.IsVisibleInTree() && _emulator.HasFocus();

	public override void _Ready()
	{
		_emulator = GetNode<TerminalSurface>("Emulator");
		_terminal = FindTerminal();
		_connectionOverlay = _terminal.GetNode<Control>("ConnectionOverlay");
		_statusLabel = _connectionOverlay.GetNode<Label>("Center/Panel/Content/StatusLabel");
		_detailLabel = _connectionOverlay.GetNode<Label>("Center/Panel/Content/DetailLabel");
		_retryButton = _connectionOverlay.GetNode<Button>("Center/Panel/Content/Buttons/RetryButton");
		_exitButton = _connectionOverlay.GetNode<Button>("Center/Panel/Content/Buttons/ExitButton");
		_sessionStatus = _terminal.GetNode<Label>("Header/HeaderMargin/HeaderRow/SessionStatus");

		_retryButton.Pressed += OnRetryPressed;
		_exitButton.Pressed += _terminal.ReturnToDesktop;
		_emulator.DataSent += OnTerminalDataSent;
		_emulator.TerminalSizeChanged += OnTerminalSizeChanged;

		_terminalController = new TerminalController
		{
			host = terminalHost,
			port = terminalPort,
			username = terminalUsername,
			password = terminalPassword,
			loginTimeoutMs = terminalLoginTimeoutMs,
			maxAttempts = terminalMaxAttempts,
			retryDelayMs = terminalRetryDelayMs,
			slowWarningAttempt = terminalSlowWarningAttempt,
			totalTimeoutMs = terminalTotalTimeoutMs
		};
		AddChild(_terminalController);
		_terminalController.OutputReceived += AppendOutput;
		_terminalController.ConnectionStarted += OnConnectionStarted;
		_terminalController.ConnectionDelayed += OnConnectionDelayed;
		_terminalController.ConnectionSucceeded += OnConnectionSucceeded;
		_terminalController.ConnectionFailed += ShowFailureState;
		_terminalController.ConnectionClosed += OnConnectionClosed;

		UpdateTerminalSize();
		SetInputEnabled(false);
	}

	public override void _ExitTree()
	{
		_lifetimeCts.Cancel();
		_retryButton.Pressed -= OnRetryPressed;
		_exitButton.Pressed -= _terminal.ReturnToDesktop;
		_lifetimeCts.Dispose();
	}

	public void EnsureStarted()
	{
		if (_terminalController.HasActiveSession)
		{
			_emulator.GrabFocus();
			return;
		}

		if (_hasRequestedInitialization)
			return;

		_hasRequestedInitialization = true;
		_ = PrepareEnvironmentAsync();
	}

	private async Task PrepareEnvironmentAsync()
	{
		if (_isPreparingEnvironment)
			return;

		_isPreparingEnvironment = true;
		ShowConnectingState(
			"Iniciando o terminal...",
			"Preparando o ambiente Docker e o terminal."
		);

		try
		{
			if (MissionInfrastructureManager.Instance == null)
			{
				ShowFailureState("O gerenciador de infraestrutura não está disponível.");
				return;
			}

			bool containerReady = await MissionInfrastructureManager.Instance
				.EnsurePlayerMachineReadyAsync(_lifetimeCts.Token);

			if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
				return;

			if (!containerReady)
			{
				ShowFailureState(
					"Não foi possível iniciar o ambiente Docker. " +
					"Verifique se o Docker está funcionando e tente novamente."
				);
				return;
			}

			UpdateTerminalSize();
			_terminalController.StartConnection();
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			Log.Error($"InputText: erro ao preparar ambiente: {exception.Message}");

			if (GodotObject.IsInstanceValid(this) && IsInsideTree())
				ShowFailureState("Ocorreu um erro ao preparar o terminal.");
		}
		finally
		{
			_isPreparingEnvironment = false;
		}
	}

	private void OnConnectionStarted()
	{
		ShowConnectingState(
			"Iniciando o terminal...",
			"Conectando ao serviço do terminal."
		);
		_emulator.ResetTerminal();
	}

	private void OnConnectionDelayed()
	{
		ShowConnectingState(
			"Ainda estamos preparando o terminal...",
			"A inicialização está demorando mais que o esperado."
		);
	}

	private void OnConnectionSucceeded()
	{
		_connectionOverlay.Visible = false;
		_sessionStatus.Text = "● CONECTADO";
		SetInputEnabled(true);

		if (IsVisibleInTree())
			_emulator.GrabFocus();
	}

	private void OnConnectionClosed()
	{
		ShowFailureState(
			"A sessão no container foi encerrada. Você pode conectar novamente.",
			"Sessão encerrada"
		);
		_retryButton.Text = "Conectar novamente";
	}

	private async void OnRetryPressed()
	{
		await PrepareEnvironmentAsync();
	}

	private void ShowConnectingState(string title, string detail)
	{
		SetInputEnabled(false);
		_connectionOverlay.Visible = true;
		_statusLabel.Text = title;
		_detailLabel.Text = detail;
		_sessionStatus.Text = "● CONECTANDO";
		_retryButton.Visible = false;
		_exitButton.Visible = true;
	}

	private void ShowFailureState(string message)
	{
		ShowFailureState(message, "Terminal indisponível");
	}

	private void ShowFailureState(string message, string title)
	{
		SetInputEnabled(false);
		_connectionOverlay.Visible = true;
		_statusLabel.Text = title;
		_detailLabel.Text = message;
		_sessionStatus.Text = "● DESCONECTADO";
		_retryButton.Text = "Tentar novamente";
		_retryButton.Visible = true;
		_exitButton.Visible = true;

		if (IsVisibleInTree())
			_retryButton.GrabFocus();
	}

	private void SetInputEnabled(bool enabled)
	{
		_inputEnabled = enabled;
		_emulator.SetInputEnabled(enabled);
	}

	private void AppendOutput(byte[] output)
	{
		_emulator.WriteData(output);
	}

	private void OnTerminalDataSent(byte[] data)
	{
		if (_inputEnabled)
			_terminalController.SendInput(data);
	}

	private void OnTerminalSizeChanged(Vector2I size)
	{
		_terminalController?.ResizeTerminal(size.X, size.Y);
	}

	private void UpdateTerminalSize()
	{
		_terminalController.ResizeTerminal(
			_emulator.Columns,
			_emulator.Rows
		);
	}

	private Terminal FindTerminal()
	{
		for (Node current = GetParent(); current != null; current = current.GetParent())
		{
			if (current is Terminal terminal)
				return terminal;
		}

		throw new InvalidOperationException("InputText: interface do terminal não encontrada.");
	}
}
