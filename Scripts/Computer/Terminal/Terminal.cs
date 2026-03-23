using Godot;

public partial class Terminal : Control
{
	DockerManager docker;
	public override async void _Ready()
	{
		Log.Info("Entrou na cena!");
		base._Ready();
		docker = new DockerManager("player_machine");
		await docker.StartAsync();
		
	}

	public override async void _ExitTree()
	{
		if (docker != null)
			await docker.StopAsync();

		base._ExitTree();
	}
	// maria vitoria
	private void _on_back_icon_bt_pressed()
	{
		GetParent<Screens>().ShowScreen("Desktop");
	}
}
