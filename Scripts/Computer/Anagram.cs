using Godot;
using System.Threading.Tasks;
using System;

public partial class Anagram : Control
{
	[Signal] public delegate void SuccessEventHandler();

	[Export] public Godot.Collections.Array<string> PossibleWords { get; set; } = new();
	[Export] public int MaxAttempts = 3;
	[Export] public float CooldownSeconds = 10.0f;

	private Label _scrambledLabel;
	private LineEdit _input;
	private Button _tryButton;
	private Label _messageLabel;

	private string _currentWord;        // palavra escolhida (senha correta)
	private string _scrambledWord;
	private int _attemptsLeft;
	private bool _onCooldown;

	public override void _Ready()
	{
		_scrambledLabel = GetNode<Label>("ScrambledLabel");
		_input = GetNode<LineEdit>("LineEdit");
		_tryButton = GetNode<Button>("TryButton");
		_messageLabel = GetNode<Label>("MessageLabel");

		_tryButton.Pressed += OnTryPressed;
		Reset();
	}

	private void Reset()
	{
		if (PossibleWords == null || PossibleWords.Count == 0)
		{
			GD.PrintErr("Anagram: Nenhuma palavra possível definida!");
			return;
		}

		// Escolhe uma palavra aleatória da lista
		var random = new Random();
		_currentWord = PossibleWords[random.Next(PossibleWords.Count)].ToUpper();

		_attemptsLeft = MaxAttempts;
		_onCooldown = false;
		_tryButton.Disabled = false;
		_messageLabel.Text = "";
		_input.Text = "";
		_scrambledWord = ScrambleWord(_currentWord);
		_scrambledLabel.Text = $"Palavra embaralhada: {_scrambledWord}";
	}

	private string ScrambleWord(string word)
	{
		char[] arr = word.ToCharArray();
		System.Random rng = new System.Random();
		for (int i = arr.Length - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(arr[i], arr[j]) = (arr[j], arr[i]);
		}
		return new string(arr);
	}

	private async void OnTryPressed()
	{
		if (_onCooldown)
		{
			_messageLabel.Text = "Aguarde o cooldown...";
			return;
		}

		string answer = _input.Text.Trim().ToUpper();
		if (answer == _currentWord)
		{
			_messageLabel.Text = "Senha correta! Conectando...";
			EmitSignal(SignalName.Success);
			return;
		}

		_attemptsLeft--;
		if (_attemptsLeft > 0)
		{
			_messageLabel.Text = $"Senha incorreta. Tentativas restantes: {_attemptsLeft}";
			_scrambledWord = ScrambleWord(_currentWord);
			_scrambledLabel.Text = $"Palavra embaralhada: {_scrambledWord}";
			_input.Text = "";
		}
		else
		{
			_messageLabel.Text = $"Tentativas esgotadas. Aguarde {CooldownSeconds} segundos...";
			_attemptsLeft = MaxAttempts;
			_onCooldown = true;
			_tryButton.Disabled = true;

			await ToSignal(GetTree().CreateTimer(CooldownSeconds), SceneTreeTimer.SignalName.Timeout);

			_onCooldown = false;
			_tryButton.Disabled = false;
			// Gera nova palavra aleatória após o cooldown
			Reset();
			_messageLabel.Text = "Novas tentativas disponíveis.";
		}
	}

	// Método público para reiniciar o anagrama (chamado pelo WiFiScreen)
	public void Restart()
	{
		Reset();
	}
}
