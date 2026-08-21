using Godot;

public partial class ComputerAccess : Area2D
{
		[Export]
		public string ComputerScenePath =
				"res://Scenes/Computer/computer.tscn";

		[Export] public string ReturnSpawnName;

		private bool _playerInside;
		private bool _isOpening;

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
				if (
						_playerInside &&
						!_isOpening &&
						Input.IsActionJustPressed("interact")
				)
				{
						EnterComputer();
				}
		}

		private void EnterComputer()
		{
				if (_isOpening)
						return;

				_isOpening = true;

				var gameManager = GameManager.Instance;

				if (gameManager?.UIContainer == null)
				{
						GD.PrintErr(
								"ComputerAccess: UIContainer não está disponível."
						);
						_isOpening = false;
						return;
				}

				if (
						gameManager.UIContainer.GetNodeOrNull<Control>(
								"Computer"
						) != null
				)
				{
						GD.Print(
								"ComputerAccess: o computador já está aberto."
						);
						_isOpening = false;
						return;
				}

				var computerScene = GD.Load<PackedScene>(
						ComputerScenePath
				);

				if (computerScene == null)
				{
						GD.PrintErr(
								$"ComputerAccess: cena não encontrada: " +
								ComputerScenePath
						);
						_isOpening = false;
						return;
				}

				var computerInstance = computerScene.Instantiate();

				if (computerInstance is not Control computerControl)
				{
						GD.PrintErr(
								"ComputerAccess: a cena do computador precisa " +
								"ter um nó raiz do tipo Control."
						);

						computerInstance.QueueFree();
						_isOpening = false;
						return;
				}

				computerControl.ProcessMode = ProcessModeEnum.Always;
				computerControl.TreeExited += OnComputerClosed;

				gameManager.UIContainer.AddChild(computerControl);
				GetTree().Paused = true;
				AudioManager.Instance?.PlayInteraction();

				GD.Print(
						"ComputerAccess: interface do computador aberta."
				);
		}

		private void OnComputerClosed()
		{
				_isOpening = false;
		}
}
