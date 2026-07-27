using Godot;

public partial class Bookshelf : Area2D
{
	[Export] public string BookId = "intro_cybersecurity"; // ID do livro
	[Export] public PackedScene BookshelfUIScene;

	private bool _playerInRange = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
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
		if (_playerInRange && @event.IsActionPressed("interact"))
		{
			OpenBookshelf();
		}
	}

	private void OpenBookshelf()
	{
		int questStage = QuestManager.Instance.GetQuestStage("tutorial");
		GD.Print($"Bookshelf: Estágio atual da missão 'tutorial': {questStage}");

		if (BookshelfUIScene == null)
		{
			GD.PrintErr("Bookshelf: BookshelfUIScene não atribuída.");
			return;
		}

		var ui = BookshelfUIScene.Instantiate<BookshelfUi>();
		ui.BookId = BookId;
		GameManager.Instance.UIContainer.AddChild(ui);
	}
}
