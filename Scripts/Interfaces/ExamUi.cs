using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ExamUi : CanvasLayer
{
	[Signal] public delegate void ExamFinishedEventHandler(bool approved);

	private static readonly Color DefaultOptionColor =
		new(0.86f, 0.94f, 0.97f, 1.0f);
	private static readonly Color SelectedOptionColor =
		new(0.72f, 0.6f, 1.0f, 1.0f);
	private static readonly Color SuccessColor =
		new(0.36f, 0.84f, 0.55f, 1.0f);
	private static readonly Color ErrorColor =
		new(0.96f, 0.38f, 0.42f, 1.0f);

	private Label _titleLabel;
	private Label _questionLabel;
	private VBoxContainer _optionsContainer;
	private Button _nextButton;
	private Label _counterLabel;
	private Label _selectionLabel;
	private Control _questionContent;
	private Control _resultCard;
	private Label _resultTitle;
	private Label _resultLabel;
	private Button _closeButton;
	private Button _resultCloseButton;

	private Quiz _quiz;
	private int _currentIndex;
	private int _correctCount;
	private int _selectedIndex = -1;
	private readonly List<Button> _optionButtons = new();
	private QuestLogUi _questLog;
	private bool _questLogWasObscured;
	private bool _questLogRestored;
	private bool _wasTreePaused;
	private bool _treePauseRestored;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_titleLabel = GetNode<Label>(
			"Root/ExamPanel/PanelMargin/Content/Header/TitleBlock/TitleLabel"
		);
		_questionLabel = GetNode<Label>(
			"Root/ExamPanel/PanelMargin/Content/QuestionContent/QuestionCard/QuestionMargin/QuestionLabel"
		);
		_optionsContainer = GetNode<VBoxContainer>(
			"Root/ExamPanel/PanelMargin/Content/QuestionContent/OptionsContainer"
		);
		_nextButton = GetNode<Button>(
			"Root/ExamPanel/PanelMargin/Content/QuestionContent/QuestionFooter/NextButton"
		);
		_counterLabel = GetNode<Label>(
			"Root/ExamPanel/PanelMargin/Content/QuestionContent/QuestionMeta/CounterLabel"
		);
		_selectionLabel = GetNode<Label>(
			"Root/ExamPanel/PanelMargin/Content/QuestionContent/SelectionLabel"
		);
		_questionContent = GetNode<Control>(
			"Root/ExamPanel/PanelMargin/Content/QuestionContent"
		);
		_resultCard = GetNode<Control>(
			"Root/ExamPanel/PanelMargin/Content/ResultCard"
		);
		_resultTitle = GetNode<Label>(
			"Root/ExamPanel/PanelMargin/Content/ResultCard/ResultMargin/ResultContent/ResultTitle"
		);
		_resultLabel = GetNode<Label>(
			"Root/ExamPanel/PanelMargin/Content/ResultCard/ResultMargin/ResultContent/ResultLabel"
		);
		_closeButton = GetNode<Button>(
			"Root/ExamPanel/PanelMargin/Content/Header/CloseButton"
		);
		_resultCloseButton = GetNode<Button>(
			"Root/ExamPanel/PanelMargin/Content/ResultCard/ResultMargin/ResultContent/ResultFooter/ResultCloseButton"
		);

		_nextButton.Pressed += OnNextPressed;
		_closeButton.Pressed += OnClosePressed;
		_resultCloseButton.Pressed += OnClosePressed;

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
			OnClosePressed();
			GetViewport().SetInputAsHandled();
		}
	}

	public void StartExam(string quizId, int questionCount)
	{
		_quiz = QuizManager.Instance?.GetRandomQuestions(quizId, questionCount);

		if (_quiz == null)
		{
			GD.PrintErr(
				$"ExamUi: não foi possível obter o quiz '{quizId}' " +
				$"com {questionCount} questões."
			);
			QueueFree();
			return;
		}

		_titleLabel.Text = string.IsNullOrWhiteSpace(_quiz.Title)
			? "Prova de cibersegurança"
			: _quiz.Title;
		_currentIndex = 0;
		_correctCount = 0;
		_resultCard.Visible = false;
		_questionContent.Visible = true;
		ShowQuestion();
	}

	private void ShowQuestion()
	{
		if (_currentIndex >= _quiz.Questions.Count)
		{
			ShowResult();
			return;
		}

		var question = _quiz.Questions[_currentIndex];
		_questionLabel.Text = question.Text;
		_counterLabel.Text =
			$"QUESTÃO {_currentIndex + 1} DE {_quiz.Questions.Count}";

		ClearOptions();

		var group = new ButtonGroup
		{
			AllowUnpress = false
		};

		for (int index = 0; index < question.Options.Count; index++)
		{
			var button = new Button
			{
				Text =
					$"{GetOptionLetter(index)}.  {question.Options[index]}",
				ToggleMode = true,
				ButtonGroup = group,
				CustomMinimumSize = new Vector2(0, 46),
				Alignment = HorizontalAlignment.Left,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};

			button.AddThemeFontSizeOverride("font_size", 15);

			int capturedIndex = index;
			button.Pressed += () =>
				OnOptionSelected(button, capturedIndex);
			_optionsContainer.AddChild(button);
			_optionButtons.Add(button);
		}

		_selectedIndex = -1;
		_selectionLabel.Text = "Escolha uma alternativa para continuar.";
		_selectionLabel.AddThemeColorOverride(
			"font_color",
			new Color(0.55f, 0.65f, 0.73f, 1.0f)
		);
		_nextButton.Disabled = true;
		_nextButton.Text = _currentIndex == _quiz.Questions.Count - 1
			? "Finalizar prova"
			: "Próxima questão";
	}

	private void ClearOptions()
	{
		foreach (var button in _optionButtons)
			button.QueueFree();

		_optionButtons.Clear();
	}

	private void OnOptionSelected(Button selected, int index)
	{
		if (!selected.ButtonPressed)
			return;

		_selectedIndex = index;

		foreach (var button in _optionButtons)
			button.AddThemeColorOverride(
				"font_color",
				DefaultOptionColor
			);

		selected.AddThemeColorOverride(
			"font_color",
			SelectedOptionColor
		);

		_selectionLabel.Text =
			$"Alternativa {GetOptionLetter(index)} selecionada.";
		_selectionLabel.AddThemeColorOverride(
			"font_color",
			SelectedOptionColor
		);
		_nextButton.Disabled = false;
	}

	private void OnNextPressed()
	{
		if (_selectedIndex < 0)
			return;

		var selected = _optionButtons.FirstOrDefault(
			button => button.ButtonPressed
		);

		if (selected == null)
			return;

		var currentQuestion = _quiz.Questions[_currentIndex];

		if (_selectedIndex == currentQuestion.CorrectIndex)
			_correctCount++;

		_currentIndex++;
		ShowQuestion();
	}

	private void ShowResult()
	{
		float percentage =
			(float)_correctCount / _quiz.Questions.Count;
		bool approved = percentage >= 0.7f;
		int percentageValue = Mathf.RoundToInt(percentage * 100.0f);

		_questionContent.Visible = false;
		_resultCard.Visible = true;
		_titleLabel.Text = "Avaliação concluída";

		_resultTitle.Text = approved
			? "APROVADO"
			: "REPROVADO";
		_resultTitle.AddThemeColorOverride(
			"font_color",
			approved ? SuccessColor : ErrorColor
		);

		_resultLabel.Text =
			$"Você acertou {_correctCount} de {_quiz.Questions.Count} " +
			$"questões ({percentageValue}%).\n\n" +
			(approved
				? "Parabéns! A missão foi concluída."
				: "Revise o material disponível na estante e tente novamente.");

		EmitSignal(SignalName.ExamFinished, approved);
	}

	private void OnClosePressed()
	{
		QueueFree();
	}

	private static string GetOptionLetter(int index)
	{
		return ((char)('A' + index)).ToString();
	}

	private void HideQuestLog()
	{
		_questLog =
			GetTree().GetFirstNodeInGroup("quest_log_ui") as QuestLogUi;

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

		if (!_wasTreePaused)
			GetTree().Paused = false;
	}
}
