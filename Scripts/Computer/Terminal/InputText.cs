using Godot;
using System;
using System.Threading.Tasks;

public partial class InputText : TextEdit
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

        private RichTextLabel outputText;
        private TerminalController terminalController;
        private readonly TerminalOutputSanitizer terminalOutputSanitizer = new();

        private Control connectionOverlay;
        private Label statusLabel;
        private Label detailLabel;
        private Button retryButton;
        private Button exitButton;

        private int exitLevels;
        private bool isPreparingEnvironment;

        public override async void _Ready()
        {
                outputText = GetNode<RichTextLabel>("../OutputText");

                Computer computer = FindComputer();

                connectionOverlay =
                        computer?.GetNodeOrNull<Control>("ConnectionOverlay");

                statusLabel = connectionOverlay?.GetNodeOrNull<Label>(
                        "Center/Panel/Content/StatusLabel"
                );

                detailLabel = connectionOverlay?.GetNodeOrNull<Label>(
                        "Center/Panel/Content/DetailLabel"
                );

                retryButton = connectionOverlay?.GetNodeOrNull<Button>(
                        "Center/Panel/Content/Buttons/RetryButton"
                );

                exitButton = connectionOverlay?.GetNodeOrNull<Button>(
                        "Center/Panel/Content/Buttons/ExitButton"
                );

                if (retryButton != null)
                        retryButton.Pressed += OnRetryPressed;

                if (exitButton != null)
                        exitButton.Pressed += ExitComputer;

                terminalController = new TerminalController
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

                AddChild(terminalController);

                terminalController.Connect(
                        TerminalController.SignalName
                                .OutputReceivedWithArgument,
                        new Callable(this, nameof(AppendOutput))
                );

                terminalController.Connect(
                        TerminalController.SignalName.ConnectionStarted,
                        new Callable(this, nameof(OnConnectionStarted))
                );

                terminalController.Connect(
                        TerminalController.SignalName.ConnectionDelayed,
                        new Callable(this, nameof(OnConnectionDelayed))
                );

                terminalController.Connect(
                        TerminalController.SignalName.ConnectionSucceeded,
                        new Callable(this, nameof(OnConnectionSucceeded))
                );

                terminalController.Connect(
                        TerminalController.SignalName.ConnectionFailed,
                        new Callable(this, nameof(OnConnectionFailed))
                );

                Reset();
                Editable = false;

                await PrepareEnvironmentAsync();
        }

        private async Task PrepareEnvironmentAsync()
        {
                if (isPreparingEnvironment)
                        return;

                isPreparingEnvironment = true;

                ShowConnectingState(
                        "Iniciando o computador...",
                        "Preparando o ambiente Docker e o terminal."
                );

                try
                {
			if (MissionInfrastructureManager.Instance == null)
			{
				ShowFailureState(
					"O gerenciador de infraestrutura não está disponível."
				);
				return;
			}

			bool containerReady =
				await MissionInfrastructureManager.Instance
					.EnsurePlayerMachineReadyAsync();

                        if (
                                !GodotObject.IsInstanceValid(this) ||
                                !IsInsideTree()
                        )
                        {
                                return;
                        }

                        if (!containerReady)
                        {
                                ShowFailureState(
                                        "Não foi possível iniciar o ambiente " +
                                        "Docker. Verifique se o Docker está " +
                                        "funcionando e tente novamente."
                                );
                                return;
                        }

                        terminalController.StartConnection();
                }
                catch (Exception exception)
                {
                        Log.Error(
                                $"InputText: erro ao preparar ambiente: " +
                                exception.Message
                        );

                        ShowFailureState(
                                "Ocorreu um erro ao preparar o computador."
                        );
                }
                finally
                {
                        isPreparingEnvironment = false;
                }
        }

        private void OnConnectionStarted()
        {
                terminalOutputSanitizer.Reset();

                ShowConnectingState(
                        "Iniciando o computador...",
                        "Conectando ao serviço do terminal."
                );
        }

        private void OnConnectionDelayed()
        {
                ShowConnectingState(
                        "Ainda estamos preparando o computador...",
                        "A inicialização está demorando mais que o esperado."
                );
        }

        private void OnConnectionSucceeded()
        {
                Editable = true;

                if (connectionOverlay != null)
                        connectionOverlay.Visible = false;

                GrabFocus();

                int tutorialStage =
                        QuestManager.Instance?.GetQuestStage("tutorial") ?? 0;

                if (tutorialStage == 3)
                {
                        QuestManager.Instance.SetQuestStage("tutorial", 4);

                        GD.Print(
                                "InputText: tutorial avançado após conexão " +
                                "bem-sucedida com o computador."
                        );
                }
        }

        private void OnConnectionFailed(string message)
        {
                ShowFailureState(message);
        }

        private async void OnRetryPressed()
        {
                await PrepareEnvironmentAsync();
        }

        private void ShowConnectingState(
                string title,
                string detail
        )
        {
                Editable = false;

                if (connectionOverlay != null)
                        connectionOverlay.Visible = true;

                if (statusLabel != null)
                        statusLabel.Text = title;

                if (detailLabel != null)
                        detailLabel.Text = detail;

                if (retryButton != null)
                        retryButton.Visible = false;

                if (exitButton != null)
                        exitButton.Visible = true;
        }

        private void ShowFailureState(string message)
        {
                Editable = false;

                if (connectionOverlay != null)
                        connectionOverlay.Visible = true;

                if (statusLabel != null)
                        statusLabel.Text = "Não foi possível acessar o computador";

                if (detailLabel != null)
                        detailLabel.Text = message;

                if (retryButton != null)
                        retryButton.Visible = true;

                if (exitButton != null)
                        exitButton.Visible = true;
        }

        private void ExitComputer()
        {
                Computer computer = FindComputer();

                if (computer != null)
                {
                        computer.ExitComputer();
                        return;
                }

                GD.PrintErr(
                        "InputText: não foi possível localizar a interface " +
                        "do computador."
                );
        }

        private void Reset()
        {
                Text = string.Empty;
        }

        private void AppendOutput(string output)
        {
                string sanitized = terminalOutputSanitizer.Process(output);

                if (!string.IsNullOrEmpty(sanitized))
                        outputText.AppendText(sanitized);
        }

        private Computer FindComputer()
        {
                Node current = this;

                while (current != null)
                {
                        if (current is Computer computer)
                                return computer;

                        current = current.GetParent();
                }

                return null;
        }

        private void ProcessCommand(string command)
        {
                terminalController.SendCommand(command);
        }

        public override void _GuiInput(InputEvent @event)
        {
                if (@event is not InputEventKey eventKey)
                        return;

                if (
                        !eventKey.Pressed ||
                        eventKey.Keycode != Key.Enter
                )
                {
                        return;
                }

                AcceptEvent();

                string command = GetLine(GetCaretLine()).Trim();

                string[] commandParts = command.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries
                );

                if (commandParts.Length == 0)
                {
                        Reset();
                        return;
                }

                switch (commandParts[0])
                {
                        case "su" or "telnet" or "ssh":
                                exitLevels++;
                                ProcessCommand(command);
                                break;

                        case "sudo":
                                if (
                                        commandParts.Length > 1 &&
                                        (
                                                commandParts[1] == "su" ||
                                                commandParts[1] == "telnet" ||
                                                commandParts[1] == "ssh"
                                        )
                                )
                                {
                                        exitLevels++;
                                }

                                ProcessCommand(command);
                                break;

                        case "exit":
                                if (exitLevels == 0)
                                {
                                        ExitComputer();
                                }
                                else
                                {
                                        exitLevels--;
                                        ProcessCommand(command);
                                }
                                break;

                        case "clear":
                                outputText.Text = string.Empty;
                                terminalOutputSanitizer.Reset();
                                break;

                        default:
                                ProcessCommand(command);
                                break;
                }

                Reset();
        }
}
