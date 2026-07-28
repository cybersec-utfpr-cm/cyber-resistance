using Godot;

public partial class Computer : Control
{
	private Control _desktop;
	private Control _terminal;
	private Control _settings;
	private Control _wifi;
	private QuestLogUi _questLog;
	private bool _questLogWasObscured;
	private bool _questLogStateCaptured;

	public override void _Ready()
	{
		_desktop = GetNode<Control>("Screens/Desktop");
		_terminal = GetNode<Control>("Screens/Terminal");
		_settings = GetNode<Control>("Screens/Settings");
		_wifi = GetNode<Control>("Screens/WiFi");

		_questLog =
			GetTree().GetFirstNodeInGroup("quest_log_ui") as QuestLogUi;

		if (_questLog != null)
		{
			_questLogWasObscured = _questLog.IsModalObscured;
			_questLogStateCaptured = true;
			_questLog.SetModalObscured(true);
		}

		// Inicia com o desktop visível
		ShowScreen(_desktop);
	}

	private void ShowScreen(Control screen)
	{
		_desktop.Visible = (screen == _desktop);
		_terminal.Visible = (screen == _terminal);
		_settings.Visible = (screen == _settings);
		_wifi.Visible = (screen == _wifi);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel")) // ESC
		{
			ExitComputer();
		}
	}

	public void ExitComputer()
	{
		RestoreQuestLogState();
		GetTree().Paused = false;
		QueueFree();
	}

	public override void _ExitTree()
	{
		RestoreQuestLogState();
	}

	private void RestoreQuestLogState()
	{
		if (
			!_questLogStateCaptured ||
			!GodotObject.IsInstanceValid(_questLog)
		)
		{
			return;
		}

		_questLog.SetModalObscured(_questLogWasObscured);
		_questLogStateCaptured = false;
	}
}
