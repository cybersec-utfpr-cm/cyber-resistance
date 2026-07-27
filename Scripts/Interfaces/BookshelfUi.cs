using Godot;
using System.Collections.Generic;

public partial class BookshelfUi : Control
{
	[Export] public string BookId { get; set; }

	[Export] public NodePath ChapterListPath { get; set; }
	[Export] public NodePath ContentLabelPath { get; set; }
	[Export] public NodePath CloseButtonPath { get; set; }

	private VBoxContainer _chapterList;
	private RichTextLabel _contentLabel;
	private Button _closeButton;

	private Book _book;

	public override void _Ready()
	{
		// Obtém referências usando os caminhos fornecidos
		if (ChapterListPath != null)
			_chapterList = GetNode<VBoxContainer>(ChapterListPath);
		else
			GD.PrintErr("BookshelfUI: ChapterListPath não definido.");

		if (ContentLabelPath != null)
			_contentLabel = GetNode<RichTextLabel>(ContentLabelPath);
		else
			GD.PrintErr("BookshelfUI: ContentLabelPath não definido.");

		if (CloseButtonPath != null)
			_closeButton = GetNode<Button>(CloseButtonPath);
		else
			GD.PrintErr("BookshelfUI: CloseButtonPath não definido.");

		if (_closeButton != null)
			_closeButton.Pressed += OnClose;

		// Verifica se os nós essenciais foram encontrados
		if (_chapterList == null || _contentLabel == null)
		{
			GD.PrintErr("BookshelfUI: nós necessários não encontrados. Verifique os caminhos.");
			return;
		}

		_book = BookManager.Instance.GetBook(BookId);
		if (_book == null)
		{
			GD.PrintErr($"BookshelfUI: Livro '{BookId}' não encontrado.");
			return;
		}

		PopulateChapters();
	}

	private void PopulateChapters()
	{
		foreach (var chapter in _book.Chapters)
		{
			// Verifica condição de desbloqueio
			if (!string.IsNullOrEmpty(chapter.UnlockCondition))
			{
				if (!ConditionEvaluator.Evaluate(chapter.UnlockCondition))
					continue;
			}

			var button = new Button();
			button.Text = chapter.Title;
			button.Pressed += () => OnChapterSelected(chapter);
			_chapterList.AddChild(button);
		}
	}


	private void OnChapterSelected(Chapter chapter)
	{
		_contentLabel.Text = chapter.Content;

		if (
			BookId == "intro_cybersecurity" &&
			chapter.Id == "chap1" &&
			QuestManager.Instance != null &&
			QuestManager.Instance.GetQuestStage("tutorial") == 2
		)
		{
			QuestManager.Instance.SetQuestStage("tutorial", 3);
			GD.Print(
				"BookshelfUI: capítulo de comandos básicos lido. Tutorial avançado para o estágio 3."
			);
		}

		if (!string.IsNullOrEmpty(chapter.OnRead))
		{
			ProcessOnRead(chapter.OnRead);
		}
	}


	private void ProcessOnRead(string command)
	{
		
		// Ignora comandos por enquanto
		GD.Print($"BookshelfUI: Comando on_read ignorado: {command}");
	}

	private void OnClose()
	{
		QueueFree();
	}
}
