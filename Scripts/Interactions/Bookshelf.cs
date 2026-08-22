using Godot;

public partial class Bookshelf : Area2D
{
	[Export] public string BookId = "intro_cybersecurity";
	[Export] public PackedScene BookshelfUIScene;

	private bool _playerInRange;
	private Label _interactHint;
	private BookshelfUi _activeUi;

	public override void _Ready()
	{
		_interactHint = GetNodeOrNull<Label>("InteractHint");
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

	}

	public override void _Input(InputEvent @event)
	{
		if (
			_playerInRange &&
			@event.IsActionPressed("interact") &&
			!@event.IsEcho()
		)
		{
			OpenBookshelf();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("Player"))
		{
			_playerInRange = true;

			if (_interactHint != null)
				_interactHint.Visible = true;
		}
	}

	private void OnBodyExited(Node body)
	{
		if (body.IsInGroup("Player"))
		{
			_playerInRange = false;

			if (_interactHint != null)
				_interactHint.Visible = false;
		}
	}

	private void OpenBookshelf()
	{
		if (
			_activeUi != null &&
			GodotObject.IsInstanceValid(_activeUi) &&
			!_activeUi.IsQueuedForDeletion()
		)
		{
			return;
		}

		int questStage = QuestManager.Instance?.GetQuestStage("tutorial") ?? 0;
		GD.Print($"Bookshelf: estágio atual do tutorial: {questStage}");

		if (BookshelfUIScene == null)
		{
			GD.PrintErr("Bookshelf: BookshelfUIScene não atribuída.");
			return;
		}

		var ui = BookshelfUIScene.Instantiate<BookshelfUi>();
		AudioManager.Instance?.PlayInteraction();
		ui.BookId = BookId;
		_activeUi = ui;
		ui.TreeExited += OnBookshelfUiClosed;
		GameManager.Instance.UIContainer.AddChild(ui);
	}

	private void OnBookshelfUiClosed()
	{
		_activeUi = null;
	}
}
