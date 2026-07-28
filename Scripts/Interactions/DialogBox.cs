using Godot;
using System.Collections.Generic;

public partial class DialogBox : CanvasLayer
{
	private Label _label;
	private Label _counterLabel;
	private Label _continueLabel;
	private List<string> _messages;
	private int _currentIndex = 0;
	private QuestLogUi _questLog;
	private bool _questLogWasCollapsed;

	[Signal] public delegate void DialogFinishedEventHandler();

	public override void _Ready()
	{
		_label = GetNodeOrNull<Label>("Root/DialogPanel/DialogMargin/DialogContent/Message");
		_counterLabel = GetNodeOrNull<Label>(
			"Root/DialogPanel/DialogMargin/DialogContent/Footer/Counter"
		);
		_continueLabel = GetNodeOrNull<Label>(
			"Root/DialogPanel/DialogMargin/DialogContent/Footer/ContinueHint"
		);
		Hide();
	}

	public void StartDialogue(List<string> messages)
	{
		_messages = messages;
		_currentIndex = 0;

		_questLog = GetTree().GetFirstNodeInGroup("quest_log_ui") as QuestLogUi;
		if (_questLog != null)
		{
			_questLogWasCollapsed = _questLog.IsCollapsed;
			_questLog.SetCollapsed(true);
		}

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

			if (_counterLabel != null)
				_counterLabel.Text = $"{_currentIndex + 1} / {_messages.Count}";

			if (_continueLabel != null)
			{
				bool isLastMessage = _currentIndex == _messages.Count - 1;
				_continueLabel.Text = isLastMessage
					? "E  FECHAR"
					: "E  CONTINUAR";
			}
		}
		else
		{
			Hide();
			RestoreQuestLog();
			EmitSignal(SignalName.DialogFinished);
		}
	}

	public void Advance()
	{
		if (!Visible) return; // Não avança se não estiver visível
		_currentIndex++;
		ShowMessage();
	}

	private void RestoreQuestLog()
	{
		if (_questLog != null && !_questLogWasCollapsed)
			_questLog.SetCollapsed(false);

		_questLog = null;
	}
}
