using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class QuestionManager : Node
{
	public static QuestionManager Instance { get; private set; }

	private List<QuestionData> _allQuestions = new();

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		LoadQuestions();
	}

	private void LoadQuestions()
	{
		string path = "res://Data/questions.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("QuestionManager: Arquivo não encontrado.");
			return;
		}

		string content = file.GetAsText();
		var json = new Json();
		var result = json.Parse(content);
		if (result != Error.Ok)
		{
			GD.PrintErr("QuestionManager: Erro ao parsear JSON.");
			return;
		}

		var data = json.Data.AsGodotDictionary();
		var questionsArray = data["questions"].AsGodotArray();

		foreach (var qVar in questionsArray)
		{
			var qDict = qVar.AsGodotDictionary();
			var question = new QuestionData
			{
				Id = qDict["id"].AsString(),
				Theme = qDict["theme"].AsString(),
				QuestionText = qDict["question"].AsString(),
				Options = new List<string>(),
				CorrectIndex = qDict["correct"].AsInt32()
			};
			var optsArray = qDict["options"].AsGodotArray();
			foreach (var opt in optsArray)
				question.Options.Add(opt.AsString());

			_allQuestions.Add(question);
		}
	}

	// Retorna uma lista de questões aleatórias de um tema, com tamanho 'count'
	public List<QuestionData> GetRandomQuestions(string theme, int count)
	{
		var filtered = _allQuestions.Where(q => q.Theme == theme).ToList();
		if (filtered.Count == 0)
		{
			GD.PrintErr($"QuestionManager: Nenhuma questão com tema '{theme}'.");
			return new List<QuestionData>();
		}

		// Embaralha e pega as primeiras 'count'
		var random = new System.Random();
		var shuffled = filtered.OrderBy(x => random.Next()).ToList();
		return shuffled.Take(count).ToList();
	}
}

public class QuestionData
{
	public string Id { get; set; }
	public string Theme { get; set; }
	public string QuestionText { get; set; }
	public List<string> Options { get; set; }
	public int CorrectIndex { get; set; }
}
