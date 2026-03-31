using System;
using Godot;

public partial class InputText : TextEdit
{
	[Export] public string terminalHost = "127.0.0.1";
	[Export] public int terminalPort = 5000;
	[Export] public string terminalUsername = "player";
	[Export] public string terminalPassword = "player";
	[Export] public int terminalMaxAttempts = 15;
	[Export] public int terminalRetryDelayMs = 1000;

	private RichTextLabel outputText;
	private Control terminal;
	private TerminalController tc;
	private int exitLevels = 0;

	private void Reset()
	{
		Text = string.Empty;
	}

	private void AppendOutput(string output)
	{
		Log.Info("Saida do comando adicionada");
		outputText.AppendText(output);
	}

	private void ProcessCommand(string command)
	{
		tc.SendCommand(command);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventKey eventKey)
			return;

		if (!eventKey.Pressed || eventKey.Keycode != Key.Enter)
			return;

		AcceptEvent();

		string command = GetLine(GetCaretLine()).Trim();
		string[] commandParts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		if (commandParts.Length == 0)
		{
			Reset();
			return;
		}

		switch (commandParts[0])
		{
			/*
			Mantem controle da escalacao de usuario para o comando "exit".
			Se estiver no nivel base, fecha a tela do terminal.
			Se estiver em usuario elevado/remoto, apenas volta um nivel.
			*/
			case "su" or "telnet" or "ssh":
				exitLevels++;
				ProcessCommand(command);
				break;

			case "sudo":
				if (commandParts.Length > 1 &&
					(commandParts[1] == "su" || commandParts[1] == "telnet" || commandParts[1] == "ssh"))
				{
					exitLevels++;
				}
				ProcessCommand(command);
				break;

			case "exit":
				if (exitLevels == 0)
				{
					terminal.QueueFree();
				}
				else
				{
					exitLevels--;
					ProcessCommand(command);
				}
				break;

			case "clear":
				outputText.Text = string.Empty;
				break;

			default:
				ProcessCommand(command);
				break;
		}

		Reset();
	}

	public override void _Ready()
	{
		outputText = GetNode<RichTextLabel>("../OutputText");
		terminal = GetNode<Control>("..");

		tc = new TerminalController
		{
			host = terminalHost,
			port = terminalPort,
			username = terminalUsername,
			password = terminalPassword,
			maxAttempts = terminalMaxAttempts,
			retryDelayMs = terminalRetryDelayMs
		};

		AddChild(tc);
		tc.Connect(
			TerminalController.SignalName.OutputReceivedWithArgument,
			new Callable(this, nameof(AppendOutput))
		);
		tc.StartConnection();

		Reset();
	}
}
