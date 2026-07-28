using Godot;

public partial class NPCRoutine : Node
{
	private const string WorldScene =
		"res://Scenes/Establishments/world.tscn";
	private const string OfficeScene =
		"res://Scenes/Establishments/office.tscn";
	private const string CafeteriaScene =
		"res://Scenes/Establishments/cafeteria.tscn";

	public Godot.Collections.Array<NPCTask> Routine = new();

	public override void _Ready()
	{
		Routine.Clear();

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = WorldScene,
			Duration = 8.0f,
			ActivityLabel = "08:00 — preparando o início do expediente"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = WorldScene,
			LocationName = "FrontDoorOffice",
			Duration = 8.0f,
			DestinationScenePath = OfficeScene,
			DestinationSpawnName = "FrontDoorSpawn",
			ActivityLabel = "08:15 — indo para o escritório"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = OfficeScene,
			LocationName = "FrontComputer",
			Duration = 8.0f,
			ActivityLabel = "08:20 — chegando ao posto de trabalho"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = OfficeScene,
			Duration = 18.0f,
			ActivityLabel = "08:30 — trabalhando no escritório",
			ActivityAnimation = "idle_up"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = OfficeScene,
			LocationName = "FrontDoorSpawn",
			Duration = 8.0f,
			DestinationScenePath = WorldScene,
			DestinationSpawnName = "FrontDoorOffice",
			ActivityLabel = "10:00 — saindo para a pausa"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = WorldScene,
			LocationName = "FrontDoorCafeteria",
			Duration = 4.0f,
			DestinationScenePath = CafeteriaScene,
			DestinationSpawnName = "FrontDoorSpawn",
			ActivityLabel = "10:05 — indo para a cafeteria"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = CafeteriaScene,
			LocationName = "FrontServiceDesk",
			Duration = 3.0f,
			ActivityLabel = "10:10 — chegando ao balcão"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = CafeteriaScene,
			Duration = 12.0f,
			ActivityLabel = "10:15 — pausa na cafeteria"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = CafeteriaScene,
			LocationName = "FrontDoorSpawn",
			Duration = 3.0f,
			DestinationScenePath = WorldScene,
			DestinationSpawnName = "FrontDoorCafeteria",
			ActivityLabel = "10:30 — encerrando a pausa"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = WorldScene,
			LocationName = "Center",
			Duration = 6.0f,
			ActivityLabel = "10:35 — retornando ao ponto de orientação"
		});
	}

	public NPCTask GetTask(int index)
	{
		if (Routine.Count == 0)
			return null;

		if (index < 0)
			index = 0;

		return Routine[index % Routine.Count];
	}

	public int GetTaskCount()
	{
		return Routine.Count;
	}
}
