using Godot;

public partial class ComputerAccess : Area2D
{
	[Export] public string ComputerScenePath = "res://Scenes/Computer/computer.tscn";
	[Export] public string ReturnSpawnName;

	private bool _playerInside = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (body.Name == "Player")
			_playerInside = true;
	}

	private void OnBodyExited(Node body)
	{
		if (body.Name == "Player")
			_playerInside = false;
	}

	public override void _Process(double delta)
	{
		if (_playerInside && Input.IsActionJustPressed("interact"))
		{
			EnterComputer();
		}
	}

	private void EnterComputer()
	{
		var computerScene = GD.Load<PackedScene>(ComputerScenePath);
		if (computerScene == null)
		{
			GD.PrintErr($"ComputerAccess: Cena não encontrada: {ComputerScenePath}");
			return;
		}

		var computerInstance = computerScene.Instantiate();
		if (computerInstance is not Control computerControl)
		{
			GD.PrintErr("ComputerAccess: A cena do computador precisa ter um nó raiz do tipo Control (ex: Panel).");
			computerInstance.QueueFree();
			return;
		}

		var uiContainer = GameManager.Instance.UIContainer;
		uiContainer.AddChild(computerControl);
		computerControl.ProcessMode = ProcessModeEnum.Always;

		// Verifica a missão tutorial
		int stage = QuestManager.Instance.GetQuestStage("tutorial");
		if (stage == 3)
		{
			QuestManager.Instance.SetQuestStage("tutorial", 4);
			GD.Print("ComputerAccess: Missão tutorial concluída! Estágio 4.");
		}


		GetTree().Paused = true;
	}
}
