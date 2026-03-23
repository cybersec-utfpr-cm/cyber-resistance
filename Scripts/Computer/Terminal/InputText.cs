using System.ComponentModel;
using System.Net;
using Godot;

public partial class InputText : TextEdit
{
	private RichTextLabel outputText;
	private string prompt;
	private Control terminal;
	private TerminalController tc;
	private int exit_levels = 0;

	private void Reset()
	{
		Text = "";
		InsertTextAtCaret(prompt);
	}

	private void AppendOutput(string output)
	{
		Log.Info("Saída do comando adicionada");
		outputText.AppendText(output);
	}

	private void ProcessCommand(string command)
	{
		tc.SendCommand(command);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventKey eventKey)
		{
			if (eventKey.Pressed && eventKey.Keycode == Key.Enter)
			{
				AcceptEvent();

				string command = GetLine(GetCaretLine())/*.Replace(prompt, "")*/.Trim();
				string[] command_v = command.Split(' ');
				switch (command_v[0])
				{
					case "":
						break;

					/*
					Aqui a ideia é manter um controle sobre a escalação de usuário, para
					ter controle sobre o comando "exit": se o jogador escrever esse comando
					no seu usuário regular, saída do terminal. Senão, apenas descerá a camada de
					usuário
					*/
					case "su" or "telnet" or "ssh":
						exit_levels++;
						ProcessCommand(command);
						break;
					case "sudo":
						if (command_v[1] == "su" || command_v[1] == "telnet" || command_v[1] == "ssh")
						{
							exit_levels++;
							ProcessCommand(command);
							break;
						}
						else
						{
							ProcessCommand(command);
							break;
						}

					case "exit":
						if (exit_levels == 0)
						{
							terminal.QueueFree();
						}
						else
						{
							exit_levels--;
							ProcessCommand(command);
						}
						break;

					// Limpa o terminal
					case "clear":
						outputText.Text = "";
						break;

					// Processa os comandos normais que não precisam de controle
					default:
						ProcessCommand(command);
						break;

				}

				Reset();
			}
		}

	}



	public override void _Ready()
	{
		outputText = GetNode<RichTextLabel>($"../OutputText");
		terminal = GetNode<Control>($"..");

		tc = new TerminalController();
		AddChild(tc);
		tc.Connect(
		TerminalController.SignalName.OutputReceivedWithArgument, // nome do sinal
		new Callable(this, nameof(AppendOutput))                 // método callback
		);
	}
}
