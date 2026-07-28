using Godot;
using System.Collections.Generic;

public partial class NPCManager : Node
{
	public static NPCManager Instance;

	private readonly List<NPCData> _npcDataList = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public void RegisterNPC(NPCMovementAI npc)
	{
		if (npc == null || FindData(npc) != null)
			return;

		_npcDataList.Add(new NPCData
		{
			NPC = npc,
			ScenePath = npc.InitialScenePath,
			SpawnName = npc.InitialSpawnName
		});
	}

	public bool PlaceNPCInActiveScene(NPCMovementAI npc)
	{
		NPCData data = FindData(npc);
		Node currentScene = GameManager.Instance?.GetCurrentScene();

		if (
			data == null
			|| currentScene == null
			|| data.ScenePath != currentScene.SceneFilePath
			|| npc.IsTransitionPending
		)
		{
			return false;
		}

		LocationManager locationManager =
			currentScene.GetNodeOrNull<LocationManager>(
				"LocationManager"
			);

		if (locationManager == null)
		{
			GD.PrintErr(
				"NPCManager: LocationManager não encontrado em "
				+ currentScene.SceneFilePath
			);
			return false;
		}

		if (npc.GetParent() != currentScene)
			npc.Reparent(currentScene, true);

		if (data.HasScenePosition)
		{
			npc.GlobalPosition = data.ScenePosition;
		}
		else if (
			locationManager.TryGetLocation(
				data.SpawnName,
				out Vector2 spawnPosition
			)
		)
		{
			npc.GlobalPosition = spawnPosition;
		}
		else
		{
			GD.PrintErr(
				$"NPCManager: spawn '{data.SpawnName}' não encontrado."
			);
		}

		data.HasScenePosition = false;
		npc.ResumeInActiveScene();
		npc.OnPlayerEnteredScene();
		return true;
	}

	public void ParkNPCsFromScene(Node scene)
	{
		if (scene == null)
			return;

		Node persistentContainer =
			GameManager.Instance.GetNodeOrNull<Node>(
				"/root/Game/NPCContainer"
			);

		if (persistentContainer == null)
		{
			GD.PrintErr(
				"NPCManager: NPCContainer persistente não encontrado."
			);
			return;
		}

		foreach (NPCData data in _npcDataList)
		{
			NPCMovementAI npc = data.NPC;

			if (
				npc == null
				|| !GodotObject.IsInstanceValid(npc)
				|| npc.GetParent() != scene
			)
			{
				continue;
			}

			data.ScenePosition = npc.GlobalPosition;
			data.HasScenePosition = true;
			npc.SuspendInInactiveScene();
			npc.Reparent(persistentContainer, true);
		}
	}

	public void CompleteSceneTransition(
		NPCMovementAI npc,
		string destinationScenePath,
		string destinationSpawnName
	)
	{
		NPCData data = FindData(npc);

		if (
			data == null
			|| string.IsNullOrEmpty(destinationScenePath)
		)
		{
			return;
		}

		GoToNextTask(npc);

		data.ScenePath = destinationScenePath;
		data.SpawnName = destinationSpawnName;
		data.HasScenePosition = false;

		Node persistentContainer =
			GameManager.Instance.GetNodeOrNull<Node>(
				"/root/Game/NPCContainer"
			);

		if (persistentContainer == null)
		{
			GD.PrintErr(
				"NPCManager: NPCContainer persistente não encontrado."
			);
			return;
		}

		npc.SuspendInInactiveScene();

		if (npc.GetParent() != persistentContainer)
			npc.Reparent(persistentContainer, true);

		npc.MarkSceneTransitionCompleted();

		if (!PlaceNPCInActiveScene(npc))
			npc.ResumeInInactiveScene();
	}

	public void SpawnNPCsForScene(string scenePath)
	{
		foreach (NPCData data in _npcDataList)
		{
			if (data.ScenePath == scenePath)
				PlaceNPCInActiveScene(data.NPC);
		}
	}

	public void RecordOffscreenArrival(
		NPCMovementAI npc,
		NPCTask task
	)
	{
		NPCData data = FindData(npc);

		if (
			data == null
			|| task == null
			|| string.IsNullOrEmpty(task.ScenePath)
			|| string.IsNullOrEmpty(task.LocationName)
		)
		{
			return;
		}

		data.ScenePath = task.ScenePath;
		data.SpawnName = task.LocationName;
		data.HasScenePosition = false;
	}

	public NPCTask GetCurrentTask(NPCMovementAI npc)
	{
		NPCData data = FindData(npc);

		if (data == null)
			return null;

		NPCRoutine routine =
			npc.GetNodeOrNull<NPCRoutine>("NPCRoutine");

		return routine?.GetTask(data.RoutineIndex);
	}

	public void GoToNextTask(NPCMovementAI npc)
	{
		NPCData data = FindData(npc);

		if (data == null)
			return;

		NPCRoutine routine =
			npc.GetNodeOrNull<NPCRoutine>("NPCRoutine");

		if (routine == null || routine.GetTaskCount() == 0)
			return;

		data.RoutineIndex =
			(data.RoutineIndex + 1) % routine.GetTaskCount();
	}

	private NPCData FindData(NPCMovementAI npc)
	{
		return _npcDataList.Find(data => data.NPC == npc);
	}
}
