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

		// Adiciona ao UIContainer (que é um CanvasLayer)
		var uiContainer = GameManager.Instance.UIContainer;
		uiContainer.AddChild(computerControl);

		// sdsDefine o ProcessMode da UI para Always para continuar funcionando mesmo com a árvore pausada
		computerControl.ProcessMode = ProcessModeEnum.Always;

		// (Opcional) Pausa o jogo
		GetTree().Paused = true;

		GD.Print("ComputerAccess: Computador aberto como UI.");
	}
}
