using Godot;
using System.Collections.Generic;

public partial class QuizUi : CanvasLayer
{
	[Export] public NodePath TitleLabelPath { get; set; }
	[Export] public NodePath ProgressLabelPath { get; set; }
	[Export] public NodePath QuestionLabelPath { get; set; }
	[Export] public NodePath OptionsContainerPath { get; set; }
	[Export] public NodePath FeedbackLabelPath { get; set; }
	[Export] public NodePath AnswerButtonPath { get; set; }
	[Export] public NodePath CloseButtonPath { get; set; }

	private static readonly Color DefaultOptionColor =
		new(0.86f, 0.94f, 0.97f, 1.0f);
	private static readonly Color SelectedOptionColor =
		new(0.35f, 0.9f, 0.88f, 1.0f);
	private static readonly Color SuccessColor =
		new(0.36f, 0.84f, 0.55f, 1.0f);
	private static readonly Color ErrorColor =
		new(0.96f, 0.38f, 0.42f, 1.0f);

	private Label _titleLabel;
	private Label _progressLabel;
	private Label _questionLabel;
	private VBoxContainer _optionsContainer;
	private Label _feedbackLabel;
	private Button _answerButton;
	private Button _closeButton;

	private Quiz _quiz;
	private int _currentQuestionIndex;
	private int _selectedOptionIndex = -1;
	private readonly List<Button> _optionButtons = new();
	private ButtonGroup _optionGroup;
	private bool _awaitingNextQuestion;
	private bool _nodesInitialized;
	private QuestLogUi _questLog;
	private bool _questLogWasObscured;
	private bool _questLogRestored;
	private bool _wasTreePaused;
	private bool _treePauseRestored;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		AddToGroup("escape_closes_overlay");
		InitializeNodes();
		HideQuestLog();
		PauseGame();
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

	private void InitializeNodes()
	{
		if (_nodesInitialized)
			return;

		_titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
		_progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
		_questionLabel = GetNodeOrNull<Label>(QuestionLabelPath);
		_optionsContainer = GetNodeOrNull<VBoxContainer>(OptionsContainerPath);
		_feedbackLabel = GetNodeOrNull<Label>(FeedbackLabelPath);
		_answerButton = GetNodeOrNull<Button>(AnswerButtonPath);
		_closeButton = GetNodeOrNull<Button>(CloseButtonPath);

		if (
			_titleLabel == null ||
			_questionLabel == null ||
			_optionsContainer == null ||
			_answerButton == null
		)
		{
			GD.PrintErr(
				"QuizUI: a cena não contém todos os nós obrigatórios."
			);
			return;
		}

		_answerButton.Pressed += OnAnswer;

		if (_closeButton != null)
			_closeButton.Pressed += OnClose;

		_nodesInitialized = true;
	}

	public void SetQuiz(Quiz quiz)
	{
		InitializeNodes();

		if (!_nodesInitialized || quiz == null)
		{
			GD.PrintErr("QuizUI: não foi possível iniciar o quiz.");
			return;
		}

		_quiz = quiz;
		_titleLabel.Text = _quiz.Title;
		_currentQuestionIndex = 0;
		ShowQuestion();
	}

	private void ShowQuestion()
	{
		if (_quiz == null)
			return;

		if (_currentQuestionIndex >= _quiz.Questions.Count)
		{
			ShowCompletion();
			return;
		}

		var question = _quiz.Questions[_currentQuestionIndex];

		if (_progressLabel != null)
		{
			_progressLabel.Text =
				$"QUESTÃO {_currentQuestionIndex + 1} DE {_quiz.Questions.Count}";
		}

		_questionLabel.Text = question.Text;
		ClearOptions();
		ClearFeedback();

		_optionGroup = new ButtonGroup
		{
			AllowUnpress = false
		};

		for (int index = 0; index < question.Options.Count; index++)
		{
			var button = new Button
			{
				Text = $"{GetOptionLetter(index)}.  {question.Options[index]}",
				ToggleMode = true,
				ButtonGroup = _optionGroup,
				CustomMinimumSize = new Vector2(0, 50),
				Alignment = HorizontalAlignment.Left,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};

			int capturedIndex = index;
			button.Pressed += () => OnOptionSelected(button, capturedIndex);
			_optionsContainer.AddChild(button);
			_optionButtons.Add(button);
		}

		_selectedOptionIndex = -1;
		_awaitingNextQuestion = false;
		_answerButton.Text = "Responder";
		_answerButton.Disabled = true;
		_answerButton.Visible = true;
	}

	private void ShowCompletion()
	{
		_titleLabel.Text = "Quiz concluído";

		if (_progressLabel != null)
			_progressLabel.Text = $"{_quiz.Questions.Count} DE {_quiz.Questions.Count}";

		_questionLabel.Text =
			"Você concluiu todas as questões. O seu progresso foi atualizado.";

		ClearOptions();
		ShowFeedback("AVALIAÇÃO FINALIZADA", SuccessColor);
		_answerButton.Visible = false;

		if (_closeButton != null)
			_closeButton.Text = "Fechar";
	}

	private void ClearOptions()
	{
		foreach (var button in _optionButtons)
			button.QueueFree();

		_optionButtons.Clear();
	}

	private void OnOptionSelected(Button selectedButton, int index)
	{
		if (_awaitingNextQuestion || !selectedButton.ButtonPressed)
			return;

		_selectedOptionIndex = index;

		foreach (var button in _optionButtons)
			button.AddThemeColorOverride("font_color", DefaultOptionColor);

		selectedButton.AddThemeColorOverride(
			"font_color",
			SelectedOptionColor
		);

		ClearFeedback();
		_answerButton.Disabled = false;
	}

	private void OnAnswer()
	{
		if (_awaitingNextQuestion)
		{
			_currentQuestionIndex++;
			ShowQuestion();
			return;
		}

		if (_selectedOptionIndex < 0)
		{
			ShowFeedback("Selecione uma alternativa.", ErrorColor);
			return;
		}

		var currentQuestion = _quiz.Questions[_currentQuestionIndex];

		if (_selectedOptionIndex != currentQuestion.CorrectIndex)
		{
			AudioManager.Instance?.PlayError();
			ShowFeedback(
				"Resposta incorreta. Revise as alternativas e tente novamente.",
				ErrorColor
			);

			_optionButtons[_selectedOptionIndex].AddThemeColorOverride(
				"font_color",
				ErrorColor
			);
			return;
		}

		ApplyQuestionReward(currentQuestion);
		AudioManager.Instance?.PlaySuccess();
		ShowFeedback("Resposta correta.", SuccessColor);

		foreach (var button in _optionButtons)
			button.Disabled = true;

		_awaitingNextQuestion = true;
		bool isLastQuestion =
			_currentQuestionIndex == _quiz.Questions.Count - 1;
		_answerButton.Text = isLastQuestion
			? "Concluir quiz"
			: "Próxima pergunta";
	}

	private void ApplyQuestionReward(Question question)
	{
		if (string.IsNullOrEmpty(question.RewardQuestStage))
			return;

		var parts = question.RewardQuestStage.Split(':');

		if (
			parts.Length == 2 &&
			int.TryParse(parts[1], out int stage)
		)
		{
			QuestManager.Instance?.SetQuestStage(parts[0], stage);
		}
	}

	private void ShowFeedback(string text, Color color)
	{
		if (_feedbackLabel == null)
			return;

		_feedbackLabel.Text = text;
		_feedbackLabel.AddThemeColorOverride("font_color", color);
	}

	private void ClearFeedback()
	{
		if (_feedbackLabel != null)
			_feedbackLabel.Text = "";
	}

	private string GetOptionLetter(int index)
	{
		return ((char)('A' + index)).ToString();
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
