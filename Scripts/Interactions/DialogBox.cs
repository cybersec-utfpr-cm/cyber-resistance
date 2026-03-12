using Godot;
using System.Collections.Generic;

public partial class DialogBox : CanvasLayer
{
	private Label _label;
	private List<string> _messages;
	private int _currentIndex = 0;

	[Signal] public delegate void DialogFinishedEventHandler();

	public override void _Ready()
	{
		_label = GetNode<Label>("ColorRect/Label");
		Hide(); // Começa invisível
	}

	public void StartDialogue(List<string> messages)
	{
		_messages = messages;
		_currentIndex = 0;
		Show();
		ShowMessage();
	}

	private void ShowMessage()
	{
		// Proteção contra label nulo
		if (_label == null)
		{
			GD.PrintErr("DialogBox: Label é nulo. Não é possível exibir mensagem.");
			return;
		}

		if (_currentIndex < _messages.Count)
		{
			_label.Text = _messages[_currentIndex];
		}
		else
		{
			Hide();
			EmitSignal(SignalName.DialogFinished);
		}
	}

	public void Advance()
	{
		if (!Visible) return; // Não avança se não estiver visível
		_currentIndex++;
		ShowMessage();
	}
}
