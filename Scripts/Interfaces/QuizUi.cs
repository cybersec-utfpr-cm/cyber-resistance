using Godot;
using System.Collections.Generic;

public partial class QuizUi : Control
{
	[Export] public NodePath TitleLabelPath { get; set; }
	[Export] public NodePath QuestionLabelPath { get; set; }
	[Export] public NodePath OptionsContainerPath { get; set; }
	[Export] public NodePath AnswerButtonPath { get; set; }
	[Export] public NodePath CloseButtonPath { get; set; }

	private Label _titleLabel;
	private Label _questionLabel;
	private VBoxContainer _optionsContainer;
	private Button _answerButton;
	private Button _closeButton;

	private Quiz _quiz;
	private int _currentQuestionIndex = 0;
	private List<Button> _optionButtons = new();

	private bool _nodesInitialized = false;

	public override void _Ready()
	{
		InitializeNodes();
	}

	private void InitializeNodes()
	{
		if (_nodesInitialized) return;

		// Obtém referências usando os caminhos fornecidos, com logs detalhados
		_titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
		if (_titleLabel == null)
			GD.PrintErr($"QuizUI: TitleLabel não encontrado no caminho: {TitleLabelPath}");

		_questionLabel = GetNodeOrNull<Label>(QuestionLabelPath);
		if (_questionLabel == null)
			GD.PrintErr($"QuizUI: QuestionLabel não encontrado no caminho: {QuestionLabelPath}");

		_optionsContainer = GetNodeOrNull<VBoxContainer>(OptionsContainerPath);
		if (_optionsContainer == null)
			GD.PrintErr($"QuizUI: OptionsContainer não encontrado no caminho: {OptionsContainerPath}");

		_answerButton = GetNodeOrNull<Button>(AnswerButtonPath);
		if (_answerButton == null)
			GD.PrintErr($"QuizUI: AnswerButton não encontrado no caminho: {AnswerButtonPath}");

		_closeButton = GetNodeOrNull<Button>(CloseButtonPath);
		if (_closeButton == null)
			GD.PrintErr($"QuizUI: CloseButton não encontrado no caminho: {CloseButtonPath}");

		// Conecta sinais apenas se os botões existirem
		if (_answerButton != null)
			_answerButton.Pressed += OnAnswer;
		if (_closeButton != null)
			_closeButton.Pressed += OnClose;

		_nodesInitialized = true;
	}

	public void SetQuiz(Quiz quiz)
	{
		// Garante que os nós foram obtidos
		InitializeNodes();

		// Verificações essenciais
		if (_titleLabel == null || _questionLabel == null || _optionsContainer == null || _answerButton == null)
		{
			GD.PrintErr("QuizUI.SetQuiz: Abortando devido à falta de nós essenciais.");
			return;
		}

		if (quiz == null)
		{
			GD.PrintErr("QuizUI.SetQuiz: Quiz recebido é nulo.");
			return;
		}

		_quiz = quiz;
		_titleLabel.Text = _quiz.Title;
		_currentQuestionIndex = 0;
		ShowQuestion();
	}

	private void ShowQuestion()
	{
		if (_quiz == null || _currentQuestionIndex >= _quiz.Questions.Count)
		{
			_questionLabel.Text = "Parabéns! Você completou o quiz.";
			ClearOptions();
			_answerButton.Disabled = true;
			return;
		}

		var q = _quiz.Questions[_currentQuestionIndex];
		_questionLabel.Text = q.Text;

		ClearOptions();

		for (int i = 0; i < q.Options.Count; i++)
		{
			var button = new Button();
			button.Text = q.Options[i];
			button.ToggleMode = true;
			button.ButtonGroup = new ButtonGroup();
			int index = i; // captura para o lambda
			button.Pressed += () => OnOptionToggled(button, index);
			_optionsContainer.AddChild(button);
			_optionButtons.Add(button);
		}

		_answerButton.Disabled = false;
	}

	private void ClearOptions()
	{
		foreach (var btn in _optionButtons)
			btn.QueueFree();
		_optionButtons.Clear();
	}

	private void OnOptionToggled(Button button, int index)
	{
		// Pode ser usado para feedback visual
	}

	private void OnAnswer()
	{
		if (_optionButtons.Count == 0)
		{
			GD.Print("Nenhuma opção disponível.");
			return;
		}

		var selected = _optionButtons.Find(b => b.ButtonPressed);
		if (selected == null)
		{
			GD.Print("Selecione uma opção.");
			return;
		}

		int selectedIndex = _optionButtons.IndexOf(selected);
		var currentQ = _quiz.Questions[_currentQuestionIndex];

		if (selectedIndex == currentQ.CorrectIndex)
		{
			GD.Print("Correto!");

			if (!string.IsNullOrEmpty(currentQ.RewardQuestStage))
			{
				var parts = currentQ.RewardQuestStage.Split(':');
				if (parts.Length == 2)
				{
					QuestManager.Instance.SetQuestStage(parts[0], int.Parse(parts[1]));
				}
			}

			_currentQuestionIndex++;
			ShowQuestion();
		}
		else
		{
			GD.Print("Errado! Tente novamente.");
		}
	}

	private void OnClose()
	{
		QueueFree();
	}
}
