using Godot;

/// Gatilho 2D para iniciar a atividade cooperativa (caso a sala passe pelo mundo 2D).
/// Port em C# de prototype/scripts/world_scripts/minigame_starter.gd.
public partial class MinigameStarter : Area2D
{
	private const string MinigameScene = "res://prototype/scenes/in_game_reusables/minigame_quiz.tscn";
	private const int RequiredPlayers = 4;

	private Label _prompt;
	private Node2D _playerInside;
	private Node _gamePacketHandler;
	private Node _clientPacketHandler;
	private Node _gameManager;

	public override void _Ready()
	{
		_prompt = GetNode<Label>("Prompt");
		_prompt.Visible = false;
		_gamePacketHandler = GetNodeOrNull("/root/GamePacketHandler");
		_clientPacketHandler = GetNodeOrNull("/root/ClientPacketHandler");
		_gameManager = GetNodeOrNull("/root/GameManager");

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		bool isHost = _gamePacketHandler != null && _gamePacketHandler.Get("is_host").AsBool();
		if (body.IsInGroup("player") && body.Get("is_authority").AsBool() && isHost)
		{
			_playerInside = body;
			_prompt.Visible = true;
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body == _playerInside)
		{
			_playerInside = null;
			_prompt.Visible = false;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_playerInside == null || !@event.IsActionPressed("interact"))
		{
			return;
		}
		if (_gamePacketHandler == null || !_gamePacketHandler.Get("is_host").AsBool())
		{
			return;
		}
		Godot.Collections.Array spawnedIds = _clientPacketHandler != null
			? _clientPacketHandler.Get("spawned_ids").AsGodotArray()
			: new Godot.Collections.Array();
		if (spawnedIds.Count < RequiredPlayers)
		{
			GD.PushWarning($"Minigame requer {RequiredPlayers} jogadores conectados.");
			return;
		}
		if (_gamePacketHandler.Get("host_connection").AsGodotObject() is ENetConnection hostConn)
		{
			SceneForcePacket.Create(MinigameScene).Broadcast(hostConn);
		}
		_gameManager?.Call("goto_scene", MinigameScene);
	}
}
