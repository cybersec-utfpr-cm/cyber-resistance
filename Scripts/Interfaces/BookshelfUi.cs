using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BookshelfUi : Control
{
	[Export] public string BookId { get; set; } = "intro_cybersecurity";
	[Export] public NodePath LibraryPanelPath { get; set; }
	[Export] public NodePath LibraryToggleButtonPath { get; set; }
	[Export] public NodePath BookListPath { get; set; }
	[Export] public NodePath ChapterListPath { get; set; }
	[Export] public NodePath BookTitleLabelPath { get; set; }
	[Export] public NodePath BookCountLabelPath { get; set; }
	[Export] public NodePath ContentLabelPath { get; set; }
	[Export] public NodePath CloseButtonPath { get; set; }

	private PanelContainer _libraryPanel;
	private Button _libraryToggleButton;
	private VBoxContainer _bookList;
	private VBoxContainer _chapterList;
	private Label _bookTitleLabel;
	private Label _bookCountLabel;
	private RichTextLabel _contentLabel;
	private Button _closeButton;
	private readonly Dictionary<string, Button> _bookButtons = new();
	private readonly List<Button> _chapterButtons = new();
	private ButtonGroup _bookButtonGroup;
	private ButtonGroup _chapterButtonGroup;
	private Book _selectedBook;
	private QuestLogUi _questLog;
	private bool _questLogWasObscured;
	private bool _questLogRestored;
	private bool _wasTreePaused;
	private bool _treePauseRestored;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_libraryPanel = GetNodeOrNull<PanelContainer>(LibraryPanelPath);
		_libraryToggleButton = GetNodeOrNull<Button>(LibraryToggleButtonPath);
		_bookList = GetNodeOrNull<VBoxContainer>(BookListPath);
		_chapterList = GetNodeOrNull<VBoxContainer>(ChapterListPath);
		_bookTitleLabel = GetNodeOrNull<Label>(BookTitleLabelPath);
		_bookCountLabel = GetNodeOrNull<Label>(BookCountLabelPath);
		_contentLabel = GetNodeOrNull<RichTextLabel>(ContentLabelPath);
		_closeButton = GetNodeOrNull<Button>(CloseButtonPath);

		if (
			_libraryPanel == null ||
			_libraryToggleButton == null ||
			_bookList == null ||
			_chapterList == null ||
			_bookTitleLabel == null ||
			_contentLabel == null
		)
		{
			GD.PrintErr(
				"BookshelfUI: a cena não contém todos os nós obrigatórios."
			);
			return;
		}

		if (_closeButton != null)
			_closeButton.Pressed += OnClose;

		_libraryToggleButton.Pressed += OnLibraryToggle;
		HideQuestLog();
		PauseGame();
		PopulateBooks();
	}

	public override void _ExitTree()
	{
		RestoreQuestLog();
		RestoreGamePause();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && !@event.IsEcho())
		{
			OnClose();
			GetViewport().SetInputAsHandled();
		}
	}

	private void PopulateBooks()
	{
		ClearChildren(_bookList);
		_bookButtons.Clear();

		var availableBooks =
			BookManager.Instance?.GetAvailableBooks() ?? new List<Book>();

		if (_bookCountLabel != null)
		{
			string suffix = availableBooks.Count == 1 ? "livro" : "livros";
			_bookCountLabel.Text = $"{availableBooks.Count} {suffix}";
		}

		if (availableBooks.Count == 0)
		{
			AddEmptyBookLabel();
			return;
		}

		_bookButtonGroup = new ButtonGroup
		{
			AllowUnpress = false
		};

		foreach (var book in availableBooks)
		{
			var button = new Button
			{
				Text = $"▌  {book.Title}",
				ToggleMode = true,
				ButtonGroup = _bookButtonGroup,
				CustomMinimumSize = new Vector2(0, 54),
				Alignment = HorizontalAlignment.Left,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};

			button.Pressed += () => SelectBook(book);
			_bookList.AddChild(button);
			_bookButtons[book.Id] = button;
		}

		var initialBook = availableBooks.FirstOrDefault(
			book => book.Id == BookId
		) ?? availableBooks[0];

		_bookButtons[initialBook.Id].ButtonPressed = true;
		SelectBook(initialBook);
	}

	private void SelectBook(Book book)
	{
		_selectedBook = book;
		_bookTitleLabel.Text = book.Title;
		_contentLabel.Text =
			"Selecione um capítulo ao lado para começar a leitura.";

		ClearChapters();

		_chapterButtonGroup = new ButtonGroup
		{
			AllowUnpress = false
		};

		foreach (var chapter in book.Chapters)
		{
			if (
				!string.IsNullOrEmpty(chapter.UnlockCondition) &&
				!ConditionEvaluator.Evaluate(chapter.UnlockCondition)
			)
			{
				continue;
			}

			var button = new Button
			{
				Text = chapter.Title,
				ToggleMode = true,
				ButtonGroup = _chapterButtonGroup,
				CustomMinimumSize = new Vector2(0, 46),
				Alignment = HorizontalAlignment.Left,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};

			button.Pressed += () => OnChapterSelected(chapter);
			_chapterList.AddChild(button);
			_chapterButtons.Add(button);
		}

		if (_chapterButtons.Count == 0)
			AddEmptyChapterLabel();
	}

	private void OnChapterSelected(Chapter chapter)
	{
		_contentLabel.Text = chapter.Content;

		if (
			_selectedBook?.Id == "intro_cybersecurity" &&
			chapter.Id == "chap1" &&
			QuestManager.Instance != null &&
			QuestManager.Instance.GetQuestStage("tutorial") == 2
		)
		{
			QuestManager.Instance.SetQuestStage("tutorial", 3);
			GD.Print(
				"BookshelfUI: capítulo de comandos básicos lido. " +
				"Tutorial avançado para o estágio 3."
			);
		}

		if (!string.IsNullOrEmpty(chapter.OnRead))
			ProcessOnRead(chapter.OnRead);
	}

	private void ClearChapters()
	{
		ClearChildren(_chapterList);
		_chapterButtons.Clear();
	}

	private void ClearChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void AddEmptyBookLabel()
	{
		var label = new Label
		{
			Text = "Nenhum livro disponível.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_bookList.AddChild(label);
	}

	private void AddEmptyChapterLabel()
	{
		var label = new Label
		{
			Text = "Nenhum capítulo disponível.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_chapterList.AddChild(label);
	}

	private void ProcessOnRead(string command)
	{
		GD.Print($"BookshelfUI: comando on_read ignorado: {command}");
	}

	private void OnLibraryToggle()
	{
		_libraryPanel.Visible = !_libraryPanel.Visible;
		_libraryToggleButton.Text = _libraryPanel.Visible
			? "Guardar livros"
			: "Mostrar livros";
	}

	private void HideQuestLog()
	{
		_questLog = GetTree().GetFirstNodeInGroup("quest_log_ui") as QuestLogUi;

		if (_questLog == null)
			return;

		_questLogWasObscured = _questLog.IsModalObscured;
		_questLog.SetModalObscured(true);
	}

	private void RestoreQuestLog()
	{
		if (_questLogRestored)
			return;

		_questLogRestored = true;

		if (_questLog != null && !_questLogWasObscured)
			_questLog.SetModalObscured(false);
	}

	private void PauseGame()
	{
		_wasTreePaused = GetTree().Paused;
		GetTree().Paused = true;
	}

	private void RestoreGamePause()
	{
		if (_treePauseRestored)
			return;

		_treePauseRestored = true;
		GetTree().Paused = _wasTreePaused;
	}

	private void OnClose()
	{
		RestoreQuestLog();
		RestoreGamePause();
		QueueFree();
	}
}
