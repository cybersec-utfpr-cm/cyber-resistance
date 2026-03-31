using Godot;
using System;

public partial class GameManager : Node {

	[Export] public string PlayerMachineContainerName = "player_machine";

	private string _nextSpawnName = "";
	private DockerManager _docker;

	public static GameManager Instance;
	public Node WorldContainer;
	public Node UIContainer;

	public override async void _Ready() {
		Instance = this;
		WorldContainer = GetNode("/root/Game/WorldContainer");
		UIContainer = GetNode("/root/Game/UIContainer");
		if (WorldContainer == null) {
			GD.PrintErr("GameManager.cs: WorldContainer não encontrado!");
		}

		_docker = new DockerManager(PlayerMachineContainerName);
		try
		{
			await _docker.StartAsync();
			GD.Print($"GameManager: Container '{PlayerMachineContainerName}' iniciado.");
		}
		catch (Exception e)
		{
			GD.PrintErr($"GameManager: Falha ao iniciar container '{PlayerMachineContainerName}': {e.Message}");
		}
	}

	public override async void _ExitTree()
	{
		if (_docker != null)
		{
			try
			{
				await _docker.StopAsync();
				GD.Print($"GameManager: Container '{PlayerMachineContainerName}' parado.");
			}
			catch (Exception e)
			{
				GD.PrintErr($"GameManager: Falha ao parar container '{PlayerMachineContainerName}': {e.Message}");
			}
			finally
			{
				_docker.Dispose();
			}
		}
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
