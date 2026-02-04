using Godot;
using System;

public partial class NPCMovementAI : CharacterBody2D
{
	[Export] public float Speed = 60f;

	// Esse é o nó que contém a lista de tarefas (NPCRoutine), não NPCTask
	private NPCRoutine _routine;
	private AnimatedSprite2D _sprite;
	private Timer _waitTimer;

	private bool _performingTask = false;

	public override void _Ready()
	{
		// Ajuste o caminho se o seu NPCRoutine estiver em outro local da árvore
		_routine = GetNodeOrNull<NPCRoutine>("NPCRoutine");
		if (_routine == null)
			GD.PrintErr("NPCRoutine node not found. Verifique o caminho e se o script NPCRoutine.cs está anexado.");

		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_sprite == null)
			GD.PrintErr("AnimatedSprite2D not found.");

		_waitTimer = new Timer();
		AddChild(_waitTimer);
		_waitTimer.Timeout += OnWaitFinished;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_routine == null) return; // evita NullReference se não houver rotina

		if (_performingTask) return;

		var task = _routine.GetCurrentTask();
		if (task == null) return;

		switch (task.Type)
		{
			case NPCTask.TaskType.GoTo:
				DoGoTo(delta, task);
				break;
			case NPCTask.TaskType.Wait:
				DoWait(task);
				break;
			case NPCTask.TaskType.Interact:
				DoInteract(task);
				break;
		}
	}

	private void DoGoTo(double delta, NPCTask task)
	{
		// Movimento simples sem NavigationAgent (altere se usar pathfinding)
		Vector2 direction = (task.TargetPosition - GlobalPosition);
		if (direction.Length() > 1f)
		{
			direction = direction.Normalized();
			Velocity = direction * Speed;
			MoveAndSlide();

			Animate(direction);
		}
		else
		{
			Velocity = Vector2.Zero;
			_routine.GoToNextTask();
		}
	}

	private void DoWait(NPCTask task)
	{
		_performingTask = true;
		Velocity = Vector2.Zero;
		_sprite?.Play("idle");

		_waitTimer.WaitTime = task.Duration > 0 ? task.Duration : 1.0f;
		_waitTimer.Start();
	}

	private void DoInteract(NPCTask task)
	{
		_performingTask = true;
		Velocity = Vector2.Zero;
		_sprite?.Play("idle");

		GD.Print($"Interacting with {task.TargetNPC}");
		// Aqui você pode implementar lógica de abrir diálogo etc.

		_waitTimer.WaitTime = 2.0f;
		_waitTimer.Start();
	}

	private void OnWaitFinished()
	{
		_performingTask = false;
		_routine.GoToNextTask();
	}

	private void Animate(Vector2 dir)
	{
		if (_sprite == null) return;

		if (dir == Vector2.Zero)
		{
			_sprite.Play("idle");
			return;
		}

		if (Math.Abs(dir.X) > Math.Abs(dir.Y))
			_sprite.Play(dir.X > 0 ? "right" : "left");
		else
			_sprite.Play(dir.Y > 0 ? "down" : "up");
	}
}
