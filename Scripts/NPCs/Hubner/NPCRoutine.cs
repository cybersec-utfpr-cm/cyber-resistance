using Godot;

public partial class NPCRoutine : Node
{
	[Export]
	public Godot.Collections.Array<NPCTask> Routine { get; set; } = new();

	public int CurrentTaskIndex { get; private set; } = 0;

	public NPCTask GetCurrentTask()
	{
		if (Routine.Count == 0)
			return null;

		return Routine[CurrentTaskIndex];
	}

	public void GoToNextTask()
	{
		CurrentTaskIndex++;

		if (CurrentTaskIndex >= Routine.Count)
			CurrentTaskIndex = 0;
	}
	public override void _Ready()
	{
		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			TargetPosition = new Vector2(300, 300)
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			Duration = 2
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			TargetPosition = new Vector2(50, 50)
		});
	}
}
