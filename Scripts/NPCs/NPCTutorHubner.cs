using Godot;

public partial class NPCTutorHubner : Node2D
{
	[Export] public string NpcId; // Preencher no Inspector com o ID correspondente ao JSON

	private bool _playerInRange = false;

	public override void _Ready()
	{
		var area = GetNode<Area2D>("InteractionArea");
		area.BodyEntered += OnBodyEntered;
		area.BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("Player"))
			_playerInRange = true;
	}

	private void OnBodyExited(Node body)
	{
		if (body.IsInGroup("Player"))
			_playerInRange = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!_playerInRange) return;
		if (!@event.IsActionPressed("interact")) return;

		if (DialogueManager.Instance.IsDialogueActive())
		{
			DialogueManager.Instance.AdvanceDialogue();
		}
		else
		{
			DialogueManager.Instance.StartDialogue(NpcId);
		}
	}
}
