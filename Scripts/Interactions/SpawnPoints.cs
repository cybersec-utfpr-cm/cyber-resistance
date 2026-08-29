using Godot;

public partial class SpawnPoints : Node2D
{
	public override async void _Ready()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (!GetTree().HasMeta("spawn_name"))
			return;

		string spawnName = GetTree().GetMeta("spawn_name").AsString();
		GetTree().RemoveMeta("spawn_name");

		// Busca recursiva em todos os descendentes.
		var spawn = FindChild(spawnName, true, false) as Marker2D;
		if (spawn == null)
		{
			GD.PrintErr($"Spawn '{spawnName}' não encontrado em {Name}");
			return;
		}

		var player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
		if (player == null)
		{
			GD.PrintErr("Player não encontrado na cena");
			return;
		}

		player.GlobalPosition = spawn.GlobalPosition;
		GD.Print($"Player spawnado em: {spawn.Name}");
	}
}
