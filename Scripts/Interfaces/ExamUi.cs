using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ExamUi : CanvasLayer
{
	[Signal] public delegate void ExamFinishedEventHandler(bool approved);

	private Label _questionLabel;
	private VBoxContainer _optionsContainer;
	private Button _nextButton;
	private Label _counterLabel;
	private Label _resultLabel;
	private Button _closeButton;

	private Quiz _quiz;
	private int _currentIndex = 0;
	private int _correctCount = 0;
	private List<Button> _optionButtons = new();

	public override void _Ready()
	{
		// Obtém os nós
		_questionLabel = GetNode<Label>("Panel/QuestionLabel");
		_optionsContainer = GetNode<VBoxContainer>("Panel/OptionsContainer");
		_nextButton = GetNode<Button>("Panel/NextButton");
		_counterLabel = GetNode<Label>("Panel/CounterLabel");
		_resultLabel = GetNode<Label>("Panel/ResultLabel");
		_closeButton = GetNode<Button>("Panel/CloseButton");

		// Conecta os sinais
		_nextButton.Pressed += OnNextPressed;
		_closeButton.Pressed += OnClosePressed;

		// Inicialmente ocultar feedback
		_resultLabel.Visible = false;
		_closeButton.Visible = false;
	}

	public void StartExam(string quizId, int questionCount)
	{
		_quiz = QuizManager.Instance.GetRandomQuestions(quizId, questionCount);
		if (_quiz == null)
		{
			GD.PrintErr($"ExamUi: Não foi possível obter o quiz '{quizId}' com {questionCount} questões.");
			QueueFree();
			return;
		}

		_currentIndex = 0;
		_correctCount = 0;
		ShowQuestion();
	}

	private void ShowQuestion()
	{
		if (_currentIndex >= _quiz.Questions.Count)
		{
			// Final do exame: mostra resultado
			ShowResult();
			return;
		}

		var q = _quiz.Questions[_currentIndex];
		_questionLabel.Text = q.Text;
		_counterLabel.Text = $"Questão {_currentIndex + 1} de {_quiz.Questions.Count}";

		// Limpa os botões antigos
		foreach (var btn in _optionButtons)
			btn.QueueFree();
		_optionButtons.Clear();

		// Cria um novo ButtonGroup para garantir seleção única
		var group = new ButtonGroup();

		for (int i = 0; i < q.Options.Count; i++)
		{
			var btn = new Button();
			btn.Text = q.Options[i];
			btn.ToggleMode = true;
			btn.ButtonGroup = group;   // Todos no mesmo grupo
			int idx = i;
			btn.Pressed += () => OnOptionSelected(btn, idx);
			_optionsContainer.AddChild(btn);
			_optionButtons.Add(btn);
		}

		// Desabilita o botão "Próxima" até que uma opção seja selecionada
		_nextButton.Disabled = true;
	}

	private void OnOptionSelected(Button selected, int index)
	{
		// Habilita o botão "Próxima"
		_nextButton.Disabled = false;
	}

	private void OnNextPressed()
	{
		// Encontra a opção selecionada
		var selected = _optionButtons.FirstOrDefault(b => b.ButtonPressed);
		if (selected == null) return;

		int selectedIndex = _optionButtons.IndexOf(selected);
		var currentQ = _quiz.Questions[_currentIndex];

		if (selectedIndex == currentQ.CorrectIndex)
			_correctCount++;

		_currentIndex++;
		ShowQuestion();
	}

	private void ShowResult()
	{
		float percentage = (float)_correctCount / _quiz.Questions.Count;
		bool approved = percentage >= 0.7f;

		// Mostra o resultado
		string resultText = $"Você acertou {_correctCount} de {_quiz.Questions.Count} questões.\n";
		if (approved)
			resultText += "Parabéns! Você foi aprovado!";
		else
			resultText += "Infelizmente você foi reprovado. Estude o material na estante e tente novamente.";

		_resultLabel.Text = resultText;
		_resultLabel.Visible = true;

		// Esconde os controles de questão
		_questionLabel.Visible = false;
		_optionsContainer.Visible = false;
		_nextButton.Visible = false;
		_counterLabel.Visible = false;

		// Mostra o botão de fechar
		_closeButton.Visible = true;

		// Emite o sinal (opcional, para sistemas externos)
		EmitSignal(SignalName.ExamFinished, approved);
	}

	private void OnClosePressed()
	{
		QueueFree(); // Fecha a tela
	}
}
