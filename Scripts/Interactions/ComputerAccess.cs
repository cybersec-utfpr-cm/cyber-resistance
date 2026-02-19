using Godot;

public partial class ComputerAccess : Area2D
{
	[Export] public string ComputerScenePath = "res://Scenes/Computer/computer.tscn";
	[Export] public string ReturnSpawnName;

	private bool _playerInside = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (body.Name == "Player")
			_playerInside = true;
	}

	private void OnBodyExited(Node body)
	{
		if (body.Name == "Player")
			_playerInside = false;
	}

	public override void _Process(double delta)
	{
		if (_playerInside && Input.IsActionJustPressed("interact"))
		{
			EnterComputer();
		}
	}

	private void EnterComputer()
	{
		var tree = GetTree();

		// salva de onde veio
		tree.SetMeta("return_scene_path", GetTree().CurrentScene.SceneFilePath);
		tree.SetMeta("return_spawn_name", ReturnSpawnName);

		// vai para o computador
		tree.ChangeSceneToFile(ComputerScenePath);
	}
}
