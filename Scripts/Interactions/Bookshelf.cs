using Godot;

public partial class Bookshelf : Node2D
{
	[Export] public string BookId; // ID do livro que esta estante contém
	[Export] public PackedScene BookshelfUIScene;

	private bool _playerInRange = false;

	public override void _Ready()
	{
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
		var ui = BookshelfUIScene.Instantiate<BookshelfUi>();
		ui.BookId = BookId;
		GameManager.Instance.UIContainer.AddChild(ui);
		((CanvasLayer)GameManager.Instance.UIContainer).ProcessMode = ProcessModeEnum.Always;
	}
}
