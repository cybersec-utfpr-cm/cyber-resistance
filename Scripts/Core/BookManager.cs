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
				Title = bookDict["title"].AsString(),
				AvailableFromStart =
					bookDict.ContainsKey("available_from_start") &&
					bookDict["available_from_start"].AsBool(),
				UnlockItemId = bookDict.ContainsKey("unlock_item")
					? bookDict["unlock_item"].AsString()
					: ""
			};

			var chaptersArray = bookDict["chapters"].AsGodotArray();
			foreach (var chapVar in chaptersArray)
			{
				var chapDict = chapVar.AsGodotDictionary();
				var chapter = new Chapter
				{
					Id = chapDict["id"].AsString(),
					Title = chapDict["title"].AsString(),
					Content = chapDict["content"].AsString(),
					UnlockCondition = chapDict.ContainsKey("unlock_condition") ? chapDict["unlock_condition"].AsString() : "",
					OnRead = chapDict.ContainsKey("on_read") ? chapDict["on_read"].AsString() : ""
				};
				book.Chapters.Add(chapter);
			}
			_books[book.Id] = book;
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
			string.IsNullOrWhiteSpace(book.UnlockItemId) ||
			InventoryManager.Instance == null
		)
		{
			return false;
		}

		return InventoryManager.Instance.GetItemCount(book.UnlockItemId) > 0;
	}
}

public class Book
{
	public string Id { get; set; }
	public string Title { get; set; }
	public bool AvailableFromStart { get; set; }
	public string UnlockItemId { get; set; }
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
