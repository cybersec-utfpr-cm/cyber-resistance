using Godot;

public partial class Terminal : Control
{
	public override void _Ready()
	{
		base._Ready();
		Log.Info("Entrou na cena do terminal");
	}

	private void _on_back_icon_bt_pressed()
	{
		GetParent<Screens>().ShowScreen("Desktop");
	}
}
