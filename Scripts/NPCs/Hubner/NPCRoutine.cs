using Godot;

public partial class NPCRoutine : Node
{
	public Godot.Collections.Array<NPCTask> Routine = new();

	public override void _Ready()
	{
		Routine.Clear();

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = "res://Scenes/Establishments/world.tscn",
			LocationName = "FrontDoorCafeteria"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = "res://Scenes/Establishments/cafeteria.tscn",
			LocationName = "FrontDoorSpawn"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = "res://Scenes/Establishments/cafeteria.tscn",
			LocationName = "FrontServiceDesk"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = "res://Scenes/Establishments/world.tscn",
			LocationName = "FrontDoorSpawn"
		});
	}

	public NPCTask GetTask(int index)
	{
		if (Routine.Count == 0)
			return null;

		if (index >= Routine.Count)
			return Routine[0];

		return Routine[index];
	}

	public int GetTaskCount()
	{
		return Routine.Count;
	}
}
