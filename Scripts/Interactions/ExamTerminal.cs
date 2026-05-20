using Godot;

public partial class ExamTerminal : Area2D
{
	[Export] public PackedScene ExamUIScene; // Arraste a cena ExamUi.tscn no Inspector
	private bool _playerInRange = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("Player")) _playerInRange = true;
	}

	private void OnBodyExited(Node body)
	{
		if (body.IsInGroup("Player")) _playerInRange = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (_playerInRange && @event.IsActionPressed("interact"))
		{
			int stage = QuestManager.Instance.GetQuestStage("university_exam");
			if (stage == 2)
			{
				StartExam();
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

		var examUI = ExamUIScene.Instantiate<ExamUi>();
		if (examUI == null)
		{
			GD.PrintErr("ExamTerminal: Falha ao instanciar ExamUi.");
			return;
		}

		GetTree().Root.AddChild(examUI);
		examUI.StartExam("intro_exam", 10);
		examUI.ExamFinished += OnExamFinished; // Conecta ao sinal
	}

	private void OnExamFinished(bool approved)
	{
		if (approved)
		{
			QuestManager.Instance.SetQuestStage("university_exam", 3);
			QuestManager.Instance.CompleteQuest("university_exam");
			GD.Print("Prova aprovada! Missão concluída.");
		}
		else
		{
			GD.Print("Prova reprovada. Consulte o material na estante.");
		}
	}
}
