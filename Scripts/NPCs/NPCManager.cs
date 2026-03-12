using Godot;
using System.Collections.Generic;

public partial class NPCManager : Node {
	public static NPCManager Instance;

	private List<NPCData> _npcDataList = new();

	public override void _Ready() {
		Instance = this;
	}

	public void RegisterNPC(NPCMovementAI npc) {
		var data = new NPCData {
			NPC = npc,
			ScenePath = GameManager.Instance.GetCurrentScene().SceneFilePath,
			SpawnName = ""
		};

		_npcDataList.Add(data);
	}

	public void MoveNPCToScene(NPCMovementAI npc, string scenePath, string spawnName) {
		var data = _npcDataList.Find(d => d.NPC == npc);
		if (data == null) return;

		data.ScenePath = scenePath;
		data.SpawnName = spawnName;

		// Reparentear para o NPCContainer
		var npcContainer = GameManager.Instance.GetNode("/root/Game/NPCContainer");
		npc.GetParent()?.RemoveChild(npc);
		npcContainer.AddChild(npc);

		npc.IsChangingScene = false;
	}

	public async void SpawnNPCsForScene(string scenePath)
	{
		var world = GameManager.Instance.GetCurrentScene();
		if (world == null) return;

		// Aguarda LocationManager ficar pronto
		LocationManager locationManager = null;
		while (locationManager == null)
		{
			locationManager = world.GetNodeOrNull<LocationManager>("LocationManager");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		var npcContainer = GameManager.Instance.GetNode("/root/Game/NPCContainer");

		foreach (var data in _npcDataList)
		{
			if (data.ScenePath != scenePath) continue;

			// Se o NPC já está no mundo, ignore (ou mova?)
			if (data.NPC.GetParent() == world) continue;

			// Remove de qualquer pai atual (provavelmente NPCContainer)
			data.NPC.GetParent()?.RemoveChild(data.NPC);
			world.AddChild(data.NPC);

			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			Vector2 pos = locationManager.GetLocation(data.SpawnName);
			data.NPC.GlobalPosition = pos;
			data.NPC.IsChangingScene = false;
			
			data.NPC.ExecuteCurrentTask(); 
		}
	}
	
	public NPCTask GetCurrentTask(NPCMovementAI npc)
	{
		var data = _npcDataList.Find(d => d.NPC == npc);

		if (data == null)
			return null;

		var routine = npc.GetNode<NPCRoutine>("NPCRoutine");

		return routine.GetTask(data.RoutineIndex);
	}

	public void GoToNextTask(NPCMovementAI npc)
	{
		var data = _npcDataList.Find(d => d.NPC == npc);

		if (data == null)
			return;

		var routine = npc.GetNode<NPCRoutine>("NPCRoutine");

		data.RoutineIndex++;

		if (data.RoutineIndex >= routine.GetTaskCount())
			data.RoutineIndex = 0;
	}
}
