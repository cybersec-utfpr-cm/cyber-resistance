using Godot;

public partial class NPCRoutine : Node
{
	private const string WorldScene =
		"res://Scenes/Establishments/world.tscn";
	private const string OfficeScene =
		"res://Scenes/Establishments/office.tscn";
	private const string CafeteriaScene =
		"res://Scenes/Establishments/cafeteria.tscn";
	private const string UniversityScene =
		"res://Scenes/Establishments/university.tscn";

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
			LocationName = "WorkstationSpot",
			Duration = 8.0f,
			ActivityLabel = "08:20 — iniciando o expediente na área de trabalho"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = OfficeScene,
			Duration = 10.0f,
			ActivityLabel = "08:30 — conferindo chamados e mensagens da equipe",
			ActivityAnimation = "idle_up"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = OfficeScene,
			LocationName = "ServerInspectionSpot",
			Duration = 8.0f,
			ActivityLabel = "08:45 — indo verificar a sala de servidores"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = OfficeScene,
			Duration = 10.0f,
			ActivityLabel = "09:00 — inspecionando serviços e conexões",
			ActivityAnimation = "idle_up"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = OfficeScene,
			LocationName = "MeetingRoomSpot",
			Duration = 8.0f,
			ActivityLabel = "09:20 — indo para a sala de reuniões"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = OfficeScene,
			Duration = 10.0f,
			ActivityLabel = "09:30 — revisando prioridades com a equipe",
			ActivityAnimation = "idle_up"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = OfficeScene,
			LocationName = "ReceptionDesk",
			Duration = 6.0f,
			ActivityLabel = "09:45 — acompanhando o atendimento ao cliente"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = OfficeScene,
			Duration = 6.0f,
			ActivityLabel = "09:50 — orientando a equipe de atendimento",
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
			LocationName = "FrontDoorUniversity",
			Duration = 6.0f,
			DestinationScenePath = UniversityScene,
			DestinationSpawnName = "FrontDoorSpawn",
			ActivityLabel = "10:35 — indo para a universidade"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = UniversityScene,
			LocationName = "LectureSpot",
			Duration = 8.0f,
			ActivityLabel = "10:45 — caminhando para a sala de aula"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.Wait,
			ScenePath = UniversityScene,
			Duration = 24.0f,
			ActivityLabel = "11:00 — orientando alunos na universidade",
			ActivityAnimation = "idle_up"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = UniversityScene,
			LocationName = "FrontDoorSpawn",
			Duration = 8.0f,
			DestinationScenePath = WorldScene,
			DestinationSpawnName = "FrontDoorUniversity",
			ActivityLabel = "12:00 — saindo da universidade"
		});

		Routine.Add(new NPCTask
		{
			Type = NPCTask.TaskType.GoTo,
			ScenePath = WorldScene,
			LocationName = "Center",
			Duration = 6.0f,
			ActivityLabel = "12:10 — retornando ao ponto de orientação"
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
