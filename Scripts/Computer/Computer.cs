using Godot;

public partial class Computer : Control
{
	private Control _desktop;
	private Control _terminal;
	private Control _settings;
	private Control _wifi;

	public override void _Ready()
	{
		_desktop = GetNode<Control>("Screens/Desktop");
		_terminal = GetNode<Control>("Screens/Terminal");
		_settings = GetNode<Control>("Screens/Settings");
		_wifi = GetNode<Control>("Screens/WiFi");
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
		GetTree().Paused = false;
		QueueFree();
	}
}
