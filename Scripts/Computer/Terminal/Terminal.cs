using Godot;

public partial class Terminal : Control
{
	private InputText _inputText;

	public override void _Ready()
	{
		_inputText = GetNodeOrNull<InputText>(
			"ContentMargin/TerminalContent/InputText"
		);
		VisibilityChanged += OnVisibilityChanged;

		Log.Info("Entrou na cena do terminal");

		if (Visible)
			OnVisibilityChanged();
	}

	public override void _ExitTree()
	{
		VisibilityChanged -= OnVisibilityChanged;
	}

	public void ReturnToDesktop()
	{
		GetParent<Screens>().ShowScreen("Desktop");
	}

	private void OnVisibilityChanged()
	{
		if (Visible)
			_inputText?.EnsureStarted();
	}

	private void _on_back_icon_bt_pressed()
	{
		ReturnToDesktop();
	}
}
