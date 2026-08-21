using Godot;

public partial class Bookshelf : Area2D
{
	[Export] public string BookId = "intro_cybersecurity";
	[Export] public PackedScene BookshelfUIScene;

	private static readonly Color PageColor =
		new(0.9f, 0.77f, 0.52f, 1.0f);
	private static readonly Color[] BookColors =
	{
		new(0.58f, 0.15f, 0.15f, 1.0f),
		new(0.2f, 0.38f, 0.2f, 1.0f),
		new(0.75f, 0.43f, 0.08f, 1.0f),
		new(0.16f, 0.24f, 0.48f, 1.0f),
		new(0.7f, 0.62f, 0.42f, 1.0f)
	};
	private static readonly Vector2[] BookSlots =
	{
		new(-27, -6),
		new(-13, -6),
		new(5, -6),
		new(19, -6),
		new(-27, 3),
		new(-13, 3),
		new(5, 3),
		new(19, 3)
	};

	private bool _playerInRange;
	private Label _interactHint;
	private BookshelfUi _activeUi;

	public override void _Ready()
	{
		_interactHint = GetNodeOrNull<Label>("InteractHint");
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		if (RewardManager.Instance != null)
			RewardManager.Instance.RewardCollected += OnRewardCollected;

		QueueRedraw();
	}

	public override void _ExitTree()
	{
		if (RewardManager.Instance != null)
			RewardManager.Instance.RewardCollected -= OnRewardCollected;
	}

	public override void _Draw()
	{
		int bookCount = BookManager.Instance?.GetAvailableBooks().Count ?? 1;
		bookCount = Mathf.Clamp(bookCount, 0, BookSlots.Length);

		for (int index = 0; index < bookCount; index++)
			DrawBook(BookSlots[index], BookColors[index % BookColors.Length]);
	}

	private void DrawBook(Vector2 position, Color coverColor)
	{
		DrawRect(
			new Rect2(position + new Vector2(1, 1), new Vector2(11, 9)),
			new Color(0.025f, 0.012f, 0.015f, 0.75f)
		);
		DrawRect(new Rect2(position, new Vector2(11, 9)), coverColor);
		DrawRect(
			new Rect2(position + new Vector2(1, 1), new Vector2(9, 1)),
			coverColor.Lightened(0.22f)
		);
		DrawRect(
			new Rect2(position + new Vector2(1, 7), new Vector2(9, 1)),
			PageColor
		);
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

	private void OnRewardCollected(string questId, string rewardId)
	{
		QueueRedraw();
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
