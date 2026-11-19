using Godot;
using System.Collections.Generic;
using System.Linq;

// Carrega books.json e disponibiliza os dados.
public partial class BookManager : Node
{
	public static BookManager Instance { get; private set; }

	private Dictionary<string, Book> _books = new();

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		LoadBooks();
	}

	private void LoadBooks()
	{
		_books.Clear();

		string path = "res://Data/books.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("BookManager: Arquivo não encontrado.");
			return;
		}

		string content = file.GetAsText();
		var json = new Json();
		var result = json.Parse(content);
		if (result != Error.Ok)
		{
			GD.PrintErr("BookManager: Erro ao parsear JSON.");
			return;
		}

		var data = json.Data.AsGodotDictionary();
		var booksArray = data["books"].AsGodotArray();

		foreach (var bookVar in booksArray)
		{
			var bookDict = bookVar.AsGodotDictionary();
			var book = new Book
			{
				Id = bookDict["id"].AsString(),
				Title = bookDict.ContainsKey("title")
					? bookDict["title"].AsString()
					: "",
				AvailableFromStart =
					bookDict.ContainsKey("available_from_start") &&
					bookDict["available_from_start"].AsBool(),
				UnlockItemId = bookDict.ContainsKey("unlock_item")
					? bookDict["unlock_item"].AsString()
					: "",
				UnlockQuestId = bookDict.ContainsKey("unlock_quest_id")
					? bookDict["unlock_quest_id"].AsString()
					: "",
				MarkdownPath = bookDict.ContainsKey("markdown_path")
					? bookDict["markdown_path"].AsString()
					: ""
			};

			if (!string.IsNullOrWhiteSpace(book.MarkdownPath))
			{
				if (!TryLoadMarkdownBook(book))
					continue;
			}
			else if (bookDict.ContainsKey("chapters"))
			{
				var chaptersArray = bookDict["chapters"].AsGodotArray();
				foreach (var chapVar in chaptersArray)
				{
					var chapDict = chapVar.AsGodotDictionary();
					var chapter = new Chapter
					{
						Id = chapDict["id"].AsString(),
						Title = chapDict["title"].AsString(),
						Content = chapDict["content"].AsString(),
						UnlockCondition = chapDict.ContainsKey("unlock_condition")
							? chapDict["unlock_condition"].AsString()
							: "",
						OnRead = chapDict.ContainsKey("on_read")
							? chapDict["on_read"].AsString()
							: ""
					};
					book.Chapters.Add(chapter);
				}
			}
			else
			{
				GD.PrintErr(
					$"BookManager: Livro '{book.Id}' não possui capítulos " +
					"inline nem markdown_path."
				);
				continue;
			}

			_books[book.Id] = book;
		}
	}

	private bool TryLoadMarkdownBook(Book book)
	{
		using var file = FileAccess.Open(book.MarkdownPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr(
				$"BookManager: Material Markdown não encontrado: " +
				$"'{book.MarkdownPath}'."
			);
			return false;
		}

		try
		{
			MarkdownBookDocument document = MarkdownBookParser.Parse(file.GetAsText());
			book.Title = document.Title;

			foreach (MarkdownBookChapter markdownChapter in document.Chapters)
			{
				book.Chapters.Add(new Chapter
				{
					Id = markdownChapter.Id,
					Title = markdownChapter.Title,
					Content = markdownChapter.Content
				});
			}

			return true;
		}
		catch (System.IO.InvalidDataException exception)
		{
			GD.PrintErr(
				$"BookManager: Conteúdo Markdown inválido em " +
				$"'{book.MarkdownPath}': {exception.Message}"
			);
			return false;
		}
	}

	public Book GetBook(string bookId)
	{
		return _books.ContainsKey(bookId) ? _books[bookId] : null;
	}

	public List<Book> GetAvailableBooks()
	{
		return _books.Values
			.Where(IsBookAvailable)
			.ToList();
	}

	public bool IsBookAvailable(Book book)
	{
		if (book == null)
			return false;

		if (book.AvailableFromStart || book.Id == "intro_cybersecurity")
			return true;

		if (
			!string.IsNullOrWhiteSpace(book.UnlockQuestId) &&
			QuestManager.Instance != null &&
			(
				QuestManager.Instance.IsQuestActive(book.UnlockQuestId) ||
				QuestManager.Instance.IsQuestCompleted(book.UnlockQuestId)
			)
		)
		{
			return true;
		}

		return
			!string.IsNullOrWhiteSpace(book.UnlockItemId) &&
			InventoryManager.Instance != null &&
			InventoryManager.Instance.GetItemCount(book.UnlockItemId) > 0;
	}
}

public class Book
{
	public string Id { get; set; }
	public string Title { get; set; }
	public bool AvailableFromStart { get; set; }
	public string UnlockItemId { get; set; }
	public string UnlockQuestId { get; set; }
	public string MarkdownPath { get; set; }
	public List<Chapter> Chapters { get; set; } = new();
}

public class Chapter
{
	public string Id { get; set; }
	public string Title { get; set; }
	public string Content { get; set; }
	public string UnlockCondition { get; set; }
	public string OnRead { get; set; }
}
