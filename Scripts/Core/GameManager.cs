using Godot;

public partial class GameManager : Node {
	
	private string _nextSpawnName = "";
	
	public static GameManager Instance;
	public Node WorldContainer;
	public Node UIContainer;
	
	public override void _Ready() { 
		Instance = this;
		WorldContainer = GetNode("/root/Game/WorldContainer");
		UIContainer = GetNode("/root/Game/UIContainer");
		if (WorldContainer == null) {
			GD.PrintErr("GameManager.cs: WorldContainer não encontrado!");
		}
		
		// Inicia a missão tutorial automaticamente
		QuestManager.Instance.StartQuest("tutorial");
		GD.Print("GameManager: Missão tutorial iniciada automaticamente.");
		
		var questLog = GD.Load<PackedScene>("res://Scenes/Interfaces/quest_log_ui.tscn").Instantiate();
		AddChild(questLog);  // ou UIContainer.AddChild(questLog);
	}
	
	public Node GetWorldContainer() {
		return WorldContainer;
	}
		
	public Node GetCurrentScene() {
		if (WorldContainer.GetChildCount() == 0){
			return null;
		}
		return WorldContainer.GetChild(0);
	}
		
	public void ChangeScene(string scenePath, string spawnName = "") {
		_nextSpawnName = spawnName; // para o próximo spawn
		foreach (Node child in WorldContainer.GetChildren()) {
			child.QueueFree();
		}
		
		var packed = GD.Load<PackedScene>(scenePath);
		var newScene = packed.Instantiate();
		
		WorldContainer.AddChild(newScene);
		MovePlayerToSpawnDeferred();
		SpawnNPCsDeferred(scenePath);
	}
	
	private void MovePlayerToSpawn() {
		if(string.IsNullOrEmpty(_nextSpawnName)){
			return;
		}
		
		var currentScene = GetCurrentScene();
		var spawn = currentScene.FindChild(_nextSpawnName, true, false) as Marker2D;
		
		if (spawn == null){
			GD.PrintErr("GameManager: Spawn não encontrado: " + _nextSpawnName);
			return;
		}
		
		var player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
		
		if(player == null){
			GD.PrintErr("GameManager: Player não encontrado");
			return;
		}
		
		player.GlobalPosition = spawn.GlobalPosition;
	}
	
	private async void MovePlayerToSpawnDeferred() { // esperar a cena ficar prontasd
		await ToSignal(GetTree(),
		SceneTree.SignalName.ProcessFrame);
		
		MovePlayerToSpawn();
	}
	
	private async void SpawnNPCsDeferred(string scenePath) {
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		
		NPCManager.Instance.SpawnNPCsForScene(scenePath);
	}
	
	
}
