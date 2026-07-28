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

	public string ScenePath = "";

	public string LocationName = "";

	public float Duration;

	public string DestinationScenePath = "";

	public string DestinationSpawnName = "";

	public string ActivityLabel = "";

	public string ActivityAnimation = "";

	public bool ChangesScene()
	{
		return !string.IsNullOrEmpty(DestinationScenePath);
	}
}
