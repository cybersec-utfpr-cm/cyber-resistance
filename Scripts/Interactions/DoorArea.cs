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
		// PLAYER
		if (body.Name == "Player")
		{
			_playerInside = true;
			return;
		}

		// NPC
		if (body is NPCMovementAI npc) {
			npc.OnReachedDoor(this);
		}
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

	private void ChangeScene()
	{
		GameManager.Instance.ChangeScene(
			DestinationScenePath,
			DestinationSpawnName
		);
		
	}
}
