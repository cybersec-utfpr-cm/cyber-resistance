using Godot;

public partial class Computer : Control
{
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("ui_cancel")) // ESC
		{
			ExitComputer();
		}
	}

	private void ExitComputer()
	{
		var tree = GetTree();

		if (!tree.HasMeta("return_scene_path"))
		{
			GD.PrintErr("Nenhuma cena de retorno definida");
			return;
		}

		string scenePath = tree.GetMeta("return_scene_path").AsString();
		string spawnName = tree.GetMeta("return_spawn_name").AsString();

		// define spawn para SpawnPoints.cs usar
		tree.SetMeta("spawn_name", spawnName);

		tree.ChangeSceneToFile(scenePath);
	}
}
