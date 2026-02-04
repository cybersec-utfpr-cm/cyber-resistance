using Godot;

public partial class DoorArea : Area2D
{
	[Export] public string DestinationScenePath;
	[Export] public string DestinationSpawnName;

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
			ChangeScene();
	}

	private void ChangeScene() {
		var tree = GetTree();

		tree.SetMeta("spawn_name", DestinationSpawnName);
		tree.ChangeSceneToFile(DestinationScenePath);
	}

}
