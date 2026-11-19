using Godot;

public partial class ExamTerminal : Area2D
{
	[Export] public PackedScene ExamUIScene; // Arraste a cena ExamUi.tscn no Inspector
	private bool _playerInRange = false;
	private ExamUi _activeExamUi;
	private Label _interactHint;

	public override void _Ready()
	{
		_interactHint = GetNodeOrNull<Label>("InteractHint");
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (!body.IsInGroup("Player"))
			return;

		_playerInRange = true;
		UpdateInteractHint();
	}

	private void OnBodyExited(Node body)
	{
		if (!body.IsInGroup("Player"))
			return;

		_playerInRange = false;

		if (_interactHint != null)
			_interactHint.Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (
			_playerInRange &&
			@event.IsActionPressed("interact") &&
			!@event.IsEcho()
		)
		{
			if (
				_activeExamUi != null &&
				GodotObject.IsInstanceValid(_activeExamUi) &&
				!_activeExamUi.IsQueuedForDeletion()
			)
			{
				return;
			}

			int stage = QuestManager.Instance.GetQuestStage("university_exam");
			if (stage == 2)
			{
				StartExam();
				GetViewport().SetInputAsHandled();
			}
			else
			{
				GD.Print("A prova ainda não está disponível. Fale com o professor primeiro.");
			}
		}
	}

	private void StartExam()
	{
		if (ExamUIScene == null)
		{
			GD.PrintErr("ExamTerminal: ExamUIScene não atribuída!");
			return;
		}

		_activeExamUi = ExamUIScene.Instantiate<ExamUi>();
		if (_activeExamUi == null)
		{
			GD.PrintErr("ExamTerminal: Falha ao instanciar ExamUi.");
			return;
		}

		_activeExamUi.TreeExited += OnExamUiClosed;
		_activeExamUi.ExamFinished += OnExamFinished;
		GetTree().Root.AddChild(_activeExamUi);
		_activeExamUi.StartExam("intro_exam", 10);
	}

	private void OnExamUiClosed()
	{
		_activeExamUi = null;
	}

	private void OnExamFinished(bool approved)
	{
		if (approved)
		{
			QuestManager.Instance.SetQuestStage("university_exam", 3);
			UpdateInteractHint();
			GD.Print("Prova aprovada! Entregue o resultado ao professor Hubner.");
		}
		else
		{
			GD.Print("Prova reprovada. Consulte o material na estante.");
		}
	}

	private void UpdateInteractHint()
	{
		if (_interactHint == null)
			return;

		int stage =
			QuestManager.Instance?.GetQuestStage("university_exam") ?? -1;

		_interactHint.Text = stage switch
		{
			2 => "E  INICIAR PROVA",
			3 => "RESULTADO ENVIADO",
			_ => "FALE COM HUBNER"
		};
		_interactHint.Visible = _playerInRange;
	}
}
