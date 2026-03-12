using Godot;
using System;

public partial class NPCMovementAI : CharacterBody2D
{
	[Export] public float speed = 60f;

	public string CurrentScenePath { get; set; }
	public bool IsChangingScene = false;

	private NavigationAgent2D _agent;
	private AnimatedSprite2D _sprite;

	private bool initialized = false;
	private bool _isExecutingTask = false;

	public override async void _Ready()
	{
		AddToGroup("NPC");

		_agent = GetNode<NavigationAgent2D>("NavigationAgent2D");
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		initialized = true;

		NPCManager.Instance.RegisterNPC(this);

		ExecuteCurrentTask();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!initialized || IsChangingScene)
			return;

		if (!_agent.IsNavigationFinished())
		{
			Vector2 next = _agent.GetNextPathPosition();
			Vector2 dir = (next - GlobalPosition).Normalized();

			Velocity = dir * speed;
			MoveAndSlide();

			Animate(dir);
		}
		else if (_isExecutingTask)
		{
			// Chegou no destino
			_isExecutingTask = false;
			NPCManager.Instance.GoToNextTask(this);
			ExecuteCurrentTask();
		}
	}

	public void ExecuteCurrentTask()
	{
		var task = NPCManager.Instance.GetCurrentTask(this);

		if (task == null)
			return;

		if (task.Type == NPCTask.TaskType.GoTo)
		{
			GoToLocation(task.LocationName);
		}
		else if (task.Type == NPCTask.TaskType.Wait)
		{
			StartWait(task.Duration);
		}
	}

	private void GoToLocation(string locationName)
	{
		var locationManager = GameManager.Instance
			.GetCurrentScene()
			.GetNodeOrNull<LocationManager>("LocationManager");

		if (locationManager == null)
		{
			GD.PrintErr("LocationManager não encontrado");
			return;
		}

		Vector2 target = locationManager.GetLocation(locationName);

		_agent.TargetPosition = target;
		_isExecutingTask = true;
	}

	private async void StartWait(float duration)
	{
		Velocity = Vector2.Zero;
		_sprite?.Play("idle");

		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);

		NPCManager.Instance.GoToNextTask(this);
		ExecuteCurrentTask();
	}

	private void Animate(Vector2 dir)
	{
		if (_sprite == null)
			return;

		if (Math.Abs(dir.X) > Math.Abs(dir.Y))
			_sprite.Play(dir.X > 0 ? "right" : "left");
		else
			_sprite.Play(dir.Y > 0 ? "down" : "up");
	}

	public async void OnReachedDoor(DoorArea door) {
		if (IsChangingScene) return;

		var task = NPCManager.Instance.GetCurrentTask(this);
		if (task == null || string.IsNullOrEmpty(task.ScenePath)) return;

		IsChangingScene = true;

		Velocity = Vector2.Zero;
		_sprite?.Play("idle");

		await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);

		// Move o NPC para a nova cena (armazena o spawnName = task.LocationName)
		NPCManager.Instance.MoveNPCToScene(this, task.ScenePath, task.LocationName);

		// NÃO avança a rotina aqui! O avanço ocorrerá quando a nova cena for carregada
		// e o NPC executar _Ready novamente? Mas ele não terá _Ready chamado de novo.
		// Precisamos de um mecanismo para, ao ser spawnado, ele avançar para a próxima tarefa.
	}
}
