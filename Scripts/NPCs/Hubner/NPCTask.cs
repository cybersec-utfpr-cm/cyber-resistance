using Godot;

public partial class NPCTask : Node
{
	public enum TaskType
	{
		GoTo,
		Interact,
		Wait
	}

	public TaskType Type;
	public Vector2 TargetPosition;
	public float Duration;
	public NodePath TargetNPC;
}
