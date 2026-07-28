using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class GameManager : Node
{
		[Export] public string PlayerMachineContainerName = "player_machine";

		private string _nextSpawnName = "";
		private DockerManager _docker;
		private readonly SemaphoreSlim _dockerLock = new(1, 1);
		private bool _isExiting;

		public static GameManager Instance;
		public Node WorldContainer;
		public Node UIContainer;

		public override async void _Ready()
		{
				Instance = this;
				WorldContainer = GetNode("/root/Game/WorldContainer");
				UIContainer = GetNode("/root/Game/UIContainer");

				if (WorldContainer == null)
				{
						GD.PrintErr("GameManager.cs: WorldContainer não encontrado!");
				}

				ConfigureCameraForScene(GetCurrentScene());

				if (QuestManager.Instance != null)
				{
						QuestManager.Instance.StartQuest("tutorial");
						GD.Print("GameManager: Missão tutorial iniciada automaticamente.");
				}
				else
				{
						GD.PrintErr("GameManager: QuestManager não encontrado.");
				}

				var questLogScene = GD.Load<PackedScene>(
						"res://Scenes/Interfaces/quest_log_ui.tscn"
				);

				if (questLogScene != null)
				{
						var questLog = questLogScene.Instantiate();

						if (UIContainer != null)
								UIContainer.AddChild(questLog);
						else
								AddChild(questLog);
				}

				_docker = new DockerManager(PlayerMachineContainerName);
				await EnsurePlayerMachineStartedAsync();
		}

		public async Task<bool> EnsurePlayerMachineStartedAsync()
		{
				if (_docker == null || _isExiting)
						return false;

				await _dockerLock.WaitAsync();

				try
				{
						if (_isExiting)
								return false;

						GD.Print(
								$"GameManager: verificando container '{PlayerMachineContainerName}'."
						);

						await _docker.StartAsync();

						GD.Print(
								$"GameManager: container '{PlayerMachineContainerName}' está pronto."
						);

						return true;
				}
				catch (Exception exception)
				{
						GD.PrintErr(
								$"GameManager: falha ao preparar container " +
								$"'{PlayerMachineContainerName}': {exception.Message}"
						);

						return false;
				}
				finally
				{
						_dockerLock.Release();
				}
		}

		public override async void _ExitTree()
		{
				_isExiting = true;

				if (_docker != null)
				{
						await _dockerLock.WaitAsync();

						try
						{
								await _docker.StopAsync();

								GD.Print(
										$"GameManager: container " +
										$"'{PlayerMachineContainerName}' parado."
								);
						}
						catch (Exception exception)
						{
								GD.PrintErr(
										$"GameManager: falha ao parar container " +
										$"'{PlayerMachineContainerName}': {exception.Message}"
								);
						}
						finally
						{
								_docker.Dispose();
								_docker = null;
								_dockerLock.Release();
						}
				}

				if (Instance == this)
						Instance = null;
		}

		public Node GetWorldContainer()
		{
				return WorldContainer;
		}

		public Node GetCurrentScene()
		{
				if (WorldContainer.GetChildCount() == 0)
						return null;

				return WorldContainer.GetChild(0);
		}

		public void ChangeScene(string scenePath, string spawnName = "")
		{
				_nextSpawnName = spawnName;

				var currentScene = GetCurrentScene();
				NPCManager.Instance?.ParkNPCsFromScene(currentScene);

				foreach (Node child in WorldContainer.GetChildren())
						child.QueueFree();

				var packed = GD.Load<PackedScene>(scenePath);
				var newScene = packed.Instantiate();

				WorldContainer.AddChild(newScene);
				ConfigureCameraForScene(newScene);
				MovePlayerToSpawnDeferred();
				SpawnNPCsDeferred(scenePath);
		}

		private void ConfigureCameraForScene(Node scene)
		{
				if (scene == null)
						return;

				var player =
						GetTree().GetFirstNodeInGroup("Player") as Node2D;
				var camera = player?.GetNodeOrNull<Camera2D>("Camera2D");
				var ground =
						scene.GetNodeOrNull<TileMapLayer>("TileMaps/Ground");

				if (
						camera == null
						|| ground == null
						|| ground.TileSet == null
				)
				{
						GD.PrintErr(
								"GameManager: não foi possível configurar os limites da câmera."
						);
						return;
				}

				Rect2I usedCells = ground.GetUsedRect();

				if (usedCells.Size == Vector2I.Zero)
						return;

				Vector2 halfTile =
						(Vector2)ground.TileSet.TileSize / 2.0f;
				Vector2 localTopLeft =
						ground.MapToLocal(usedCells.Position) - halfTile;
				Vector2 localBottomRight =
						ground.MapToLocal(usedCells.End - Vector2I.One)
						+ halfTile;
				Vector2 globalTopLeft =
						ground.ToGlobal(localTopLeft);
				Vector2 globalBottomRight =
						ground.ToGlobal(localBottomRight);

				camera.LimitLeft = (int)Math.Floor(
						Math.Min(globalTopLeft.X, globalBottomRight.X)
				);
				camera.LimitTop = (int)Math.Floor(
						Math.Min(globalTopLeft.Y, globalBottomRight.Y)
				);
				camera.LimitRight = (int)Math.Ceiling(
						Math.Max(globalTopLeft.X, globalBottomRight.X)
				);
				camera.LimitBottom = (int)Math.Ceiling(
						Math.Max(globalTopLeft.Y, globalBottomRight.Y)
				);
				camera.ResetSmoothing();
		}

		private void MovePlayerToSpawn()
		{
				if (string.IsNullOrEmpty(_nextSpawnName))
						return;

				var currentScene = GetCurrentScene();
				var spawn = currentScene.FindChild(
						_nextSpawnName,
						true,
						false
				) as Marker2D;

				if (spawn == null)
				{
						GD.PrintErr(
								"GameManager: Spawn não encontrado: " + _nextSpawnName
						);
						return;
				}

				var player = GetTree().GetFirstNodeInGroup("Player") as Node2D;

				if (player == null)
				{
						GD.PrintErr("GameManager: Player não encontrado");
						return;
				}

				player.GlobalPosition = spawn.GlobalPosition;
		}

		private async void MovePlayerToSpawnDeferred()
		{
				await ToSignal(
						GetTree(),
						SceneTree.SignalName.ProcessFrame
				);

				MovePlayerToSpawn();
		}

		private async void SpawnNPCsDeferred(string scenePath)
		{
				await ToSignal(
						GetTree(),
						SceneTree.SignalName.ProcessFrame
				);

				NPCManager.Instance?.SpawnNPCsForScene(scenePath);
		}
}
