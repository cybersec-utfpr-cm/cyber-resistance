using Godot;

public partial class NPCTask : Node
{
	public enum TaskType
	{
		GoTo,
		Wait,
		Interact
	}

	public TaskType Type;

	public string ScenePath;     // ex: res://Scenes/Cafeteria.tscn

	public string LocationName;  // ex: Entrance

	public float Duration;
}
