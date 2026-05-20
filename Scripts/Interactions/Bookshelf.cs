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
		// Verifica a missão tutorial
		int questStage = QuestManager.Instance.GetQuestStage("tutorial");
		GD.Print($"Bookshelf: Estágio atual da missão 'tutorial': {questStage}");

		// Se o estágio for 1 (após falar com tutor), avança para 2
		if (questStage == 1)
		{
			QuestManager.Instance.SetQuestStage("tutorial", 2);
			GD.Print("Bookshelf: Missão tutorial avançada! Estágio agora é 2.");
		}

		// Abre a UI do livro
		if (BookshelfUIScene != null)
		{
			var ui = BookshelfUIScene.Instantiate<BookshelfUi>();
			ui.BookId = BookId;
			GameManager.Instance.UIContainer.AddChild(ui);
		}
		else
		{
			GD.PrintErr("Bookshelf: BookshelfUIScene não atribuída.");
		}
		// Avança a missão se necessário
		int stage = QuestManager.Instance.GetQuestStage("tutorial");
		if (stage == 2)
		{
			QuestManager.Instance.SetQuestStage("tutorial", 3);
			GD.Print("Bookshelf: Missão tutorial avançada para estágio 3.");
		}
	}
}
