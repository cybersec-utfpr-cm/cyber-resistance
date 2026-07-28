using Godot;
using System.Collections.Generic;

// Carrega quizzes.json e gerencia a exibição de quizzes.
public partial class QuizManager : Node
{
	public static QuizManager Instance { get; private set; }

	private Dictionary<string, Quiz> _quizzes = new();
	private QuizUi _activeQuizUi;

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		LoadQuizzes();
	}

	private void LoadQuizzes()
	{
		string path = "res://Data/quizzes.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("QuizManager: Arquivo não encontrado.");
			return;
		}

		string content = file.GetAsText();
		var json = new Json();
		var result = json.Parse(content);
		if (result != Error.Ok)
		{
			GD.PrintErr("QuizManager: Erro ao parsear JSON.");
			return;
		}

		var data = json.Data.AsGodotDictionary();
		var quizzesArray = data["quizzes"].AsGodotArray();

		foreach (var quizVar in quizzesArray)
		{
			var quizDict = quizVar.AsGodotDictionary();
			var quiz = new Quiz
			{
				Id = quizDict["id"].AsString(),
				Title = quizDict["title"].AsString()
			};

			var questionsArray = quizDict["questions"].AsGodotArray();
			foreach (var qVar in questionsArray)
			{
				var qDict = qVar.AsGodotDictionary();
				var question = new Question
				{
					Text = qDict["question"].AsString(),
					Options = new List<string>(),
					CorrectIndex = qDict["correct"].AsInt32(),
					RewardQuestStage = qDict.ContainsKey("reward_quest_stage") ? qDict["reward_quest_stage"].AsString() : ""
				};
				var optionsArray = qDict["options"].AsGodotArray();
				foreach (var opt in optionsArray)
					question.Options.Add(opt.AsString());

				quiz.Questions.Add(question);
			}
			_quizzes[quiz.Id] = quiz;
		}
	}

	public Quiz GetQuiz(string quizId)
	{
		return _quizzes.ContainsKey(quizId) ? _quizzes[quizId] : null;
	}

	public void StartQuiz(string quizId)
	{
		if (
			_activeQuizUi != null &&
			GodotObject.IsInstanceValid(_activeQuizUi) &&
			!_activeQuizUi.IsQueuedForDeletion()
		)
		{
			GD.Print("QuizManager: já existe um quiz aberto.");
			return;
		}

		var quiz = GetQuiz(quizId);
		if (quiz == null)
		{
			GD.PrintErr($"QuizManager: Quiz '{quizId}' não encontrado.");
			return;
		}

		var quizUIScene = GD.Load<PackedScene>("res://Scenes/Interfaces/quiz_ui.tscn");

		if (quizUIScene == null)
		{
			GD.PrintErr("QuizManager: cena da interface não encontrada.");
			return;
		}

		var quizUI = quizUIScene.Instantiate<QuizUi>();
		_activeQuizUi = quizUI;
		quizUI.TreeExited += OnQuizUiClosed;
		AddChild(quizUI);
		quizUI.SetQuiz(quiz);
	}

	private void OnQuizUiClosed()
	{
		_activeQuizUi = null;
	}
	
	public Quiz GetRandomQuestions(string quizId, int count)
	{
		var originalQuiz = GetQuiz(quizId);
		if (originalQuiz == null) return null;

		// Verifica se há perguntas suficientes
		if (originalQuiz.Questions.Count < count)
		{
			GD.PrintErr($"QuizManager: Banco de questões '{quizId}' tem apenas {originalQuiz.Questions.Count} questões, mas foram solicitadas {count}.");
			return null;
		}

		// Cria uma cópia do quiz para não modificar o original
		var newQuiz = new Quiz
		{
			Id = originalQuiz.Id,
			Title = originalQuiz.Title,
			Questions = new List<Question>()
		};

		// Seleciona perguntas aleatoriamente (sem repetição)
		var rng = new System.Random();
		var indices = new List<int>();
		for (int i = 0; i < originalQuiz.Questions.Count; i++) indices.Add(i);
		for (int i = 0; i < count; i++)
		{
			int randomIndex = rng.Next(indices.Count);
			int selectedIndex = indices[randomIndex];
			indices.RemoveAt(randomIndex);
			newQuiz.Questions.Add(originalQuiz.Questions[selectedIndex]);
		}

		return newQuiz;
	}
}

public class Quiz
{
	public string Id { get; set; }
	public string Title { get; set; }
	public List<Question> Questions { get; set; } = new();
}

public class Question
{
	public string Text { get; set; }
	public List<string> Options { get; set; }
	public int CorrectIndex { get; set; }
	public string RewardQuestStage { get; set; } // ex: "phishing:2"
}
