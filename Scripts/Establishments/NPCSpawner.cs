using Godot;
using System.Threading.Tasks;

public partial class NPCSpawner : Node2D
{
	[Export] public PackedScene NPCScene;
	[Export] public string SpawnLocation = "Center";

	public override void _Ready()
	{
		GD.Print("Spawner chamado! ");
		SpawnNPC();
	}

	private async void SpawnNPC()
	{
		var npc = NPCScene.Instantiate<NPCMovementAI>();

		var world = GetTree().CurrentScene.GetNode<Node2D>("WorldContainer/World");
		
		await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);

		world.AddChild(npc);

		var locationManager = world.GetNode<LocationManager>("LocationManager");
		npc.GlobalPosition = locationManager.GetLocation(SpawnLocation);

		var pos = locationManager.GetLocation(SpawnLocation);
		GD.Print("Posição do spawn: ", pos);
		npc.GlobalPosition = pos;
	}
}
