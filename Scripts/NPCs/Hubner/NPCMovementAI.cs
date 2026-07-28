using Godot;
using System;

public partial class NPCMovementAI : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 60.0f;
	[Export] public string InitialScenePath { get; set; } =
		"res://Scenes/Establishments/world.tscn";
	[Export] public string InitialSpawnName { get; set; } = "Center";
	[Export] public string NpcId { get; set; } = "hubner";

	public bool IsChangingScene { get; private set; } = true;
	public bool IsTransitionPending => _transitionStarted;

	private NavigationAgent2D _agent;
	private AnimatedSprite2D _sprite;
	private Area2D _interactionArea;
	private bool _initialized;
	private bool _isExecutingTask;
	private bool _isWaiting;
	private bool _transitionStarted;
	private bool _playerInRange;
	private bool _isInActiveScene;
	private bool _routineStarted;
	private bool _isTalkingToPlayer;
	private double _waitSecondsRemaining;
	private double _offscreenTravelSecondsRemaining;
	private double _transitionSecondsRemaining;
	private string _pendingDestinationScenePath = "";
	private string _pendingDestinationSpawnName = "";
	private Vector2 _targetPosition;
	private bool _hasTargetPosition;
	private MovementAxis _movementAxis;
	private uint _activeCollisionLayer;
	private uint _activeCollisionMask;
	private uint _activeInteractionCollisionLayer;
	private uint _activeInteractionCollisionMask;

	private const float ArrivalDistance = 4.0f;
	private const float AxisAlignmentDistance = 1.5f;
	private const float DefaultOffscreenTravelSeconds = 4.0f;

	private enum MovementAxis
	{
		None,
		Horizontal,
		Vertical
	}

	public override async void _Ready()
	{
		AddToGroup("NPC");

		_agent = GetNode<NavigationAgent2D>("NavigationAgent2D");
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_interactionArea =
			GetNodeOrNull<Area2D>("InteractionArea");

		_activeCollisionLayer = CollisionLayer;
		_activeCollisionMask = CollisionMask;

		if (_interactionArea != null)
		{
			_activeInteractionCollisionLayer =
				_interactionArea.CollisionLayer;
			_activeInteractionCollisionMask =
				_interactionArea.CollisionMask;
			_interactionArea.BodyEntered +=
				OnInteractionBodyEntered;
			_interactionArea.BodyExited +=
				OnInteractionBodyExited;
		}

		SetActiveScenePresentation(false);

		while (
			IsInsideTree()
			&& (GameManager.Instance == null || NPCManager.Instance == null)
		)
		{
			await ToSignal(
				GetTree(),
				SceneTree.SignalName.ProcessFrame
			);
		}

		if (!IsInsideTree())
			return;

		NPCManager.Instance.RegisterNPC(this);

		await ToSignal(
			GetTree(),
			SceneTree.SignalName.ProcessFrame
		);

		_initialized = true;

		if (!NPCManager.Instance.PlaceNPCInActiveScene(this))
			ResumeInInactiveScene();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_initialized)
			return;

		if (_isTalkingToPlayer)
		{
			if (
				DialogueManager.Instance != null
				&& DialogueManager.Instance.IsDialogueActive()
			)
			{
				PauseForDialogue();
				return;
			}

			_isTalkingToPlayer = false;
			RestoreCurrentTaskPresentation();
		}

		if (!_routineStarted)
			return;

		if (_transitionStarted)
		{
			ProcessSceneTransition(delta);
			return;
		}

		if (_isWaiting)
		{
			ProcessWait(delta);
			return;
		}

		if (IsChangingScene)
			return;

		if (!_isInActiveScene)
		{
			ProcessOffscreenMovement(delta);
			return;
		}

		ProcessVisibleMovement(delta);
	}

	private void ProcessVisibleMovement(double delta)
	{
		if (!_agent.IsNavigationFinished())
		{
			Vector2 nextPosition = _agent.GetNextPathPosition();
			Vector2 offset = nextPosition - GlobalPosition;
			Vector2 direction = GetCardinalDirection(offset);

			if (direction == Vector2.Zero)
				return;

			float axisDistance =
				_movementAxis == MovementAxis.Horizontal
					? Math.Abs(offset.X)
					: Math.Abs(offset.Y);
			float frameSpeed = Math.Min(
				Speed,
				axisDistance / Math.Max((float)delta, 0.001f)
			);

			Vector2 previousPosition = GlobalPosition;
			Velocity = direction * frameSpeed;
			MoveAndSlide();
			Animate(direction);

			if (
				GlobalPosition.DistanceTo(previousPosition) < 0.05f
				&& Math.Abs(offset.X) > AxisAlignmentDistance
				&& Math.Abs(offset.Y) > AxisAlignmentDistance
			)
			{
				_movementAxis =
					_movementAxis == MovementAxis.Horizontal
						? MovementAxis.Vertical
						: MovementAxis.Horizontal;
			}

			return;
		}

		StopMovement();

		if (!_isExecutingTask)
			return;

		_isExecutingTask = false;
		CompleteCurrentTask();
	}

	private void ProcessWait(double delta)
	{
		_waitSecondsRemaining -= delta;

		if (_waitSecondsRemaining > 0.0)
			return;

		_isWaiting = false;
		_waitSecondsRemaining = 0.0;
		AdvanceRoutine();
	}

	private void ProcessOffscreenMovement(double delta)
	{
		if (!_isExecutingTask)
		{
			ExecuteCurrentTask();
			return;
		}

		_offscreenTravelSecondsRemaining -= delta;

		if (_offscreenTravelSecondsRemaining > 0.0)
			return;

		_offscreenTravelSecondsRemaining = 0.0;
		_isExecutingTask = false;
		CompleteCurrentTask();
	}

	private void ProcessSceneTransition(double delta)
	{
		_transitionSecondsRemaining -= delta;

		if (_transitionSecondsRemaining > 0.0)
			return;

		_transitionSecondsRemaining = 0.0;

		if (string.IsNullOrEmpty(_pendingDestinationScenePath))
		{
			_transitionStarted = false;
			IsChangingScene = false;
			return;
		}

		NPCManager.Instance.CompleteSceneTransition(
			this,
			_pendingDestinationScenePath,
			_pendingDestinationSpawnName
		);
	}

	public override void _Input(InputEvent @event)
	{
		if (
			!_playerInRange
			|| !@event.IsActionPressed("interact")
		)
		{
			return;
		}

		if (DialogueManager.Instance.IsDialogueActive())
		{
			DialogueManager.Instance.AdvanceDialogue();
			return;
		}

		DialogueManager.Instance.StartDialogue(NpcId);

		if (DialogueManager.Instance.IsDialogueActive())
		{
			_isTalkingToPlayer = true;
			PauseForDialogue();
		}
	}

	public void ExecuteCurrentTask()
	{
		if (
			!_initialized
			|| !_routineStarted
			|| IsChangingScene
			|| _isWaiting
			|| _transitionStarted
		)
		{
			return;
		}

		NPCTask task = NPCManager.Instance.GetCurrentTask(this);

		if (task == null)
			return;

		if (_isInActiveScene)
		{
			Node currentScene = GameManager.Instance.GetCurrentScene();

			if (
				currentScene == null
				|| (
					!string.IsNullOrEmpty(task.ScenePath)
					&& task.ScenePath != currentScene.SceneFilePath
				)
			)
			{
				GD.PrintErr(
					"Hubner: a tarefa atual não pertence à cena ativa."
				);
				return;
			}
		}

		GD.Print($"Hubner: {task.ActivityLabel}");

		switch (task.Type)
		{
			case NPCTask.TaskType.GoTo:
				if (_isInActiveScene)
					GoToLocation(task.LocationName);
				else
					StartOffscreenMovement(task);
				break;
			case NPCTask.TaskType.Wait:
				StartWait(task.Duration);
				break;
			default:
				AdvanceRoutine();
				break;
		}
	}

	public void SuspendInInactiveScene()
	{
		_isInActiveScene = false;
		_playerInRange = false;
		SetActiveScenePresentation(false);
		StopMovement();

		if (_transitionStarted || _isWaiting)
			return;

		IsChangingScene = false;

		if (_isExecutingTask)
		{
			PrepareCurrentMovementForOffscreen();
			return;
		}

		ExecuteCurrentTask();
	}

	public void ResumeInActiveScene()
	{
		if (_transitionStarted)
			return;

		_isInActiveScene = true;
		SetActiveScenePresentation(true);
		IsChangingScene = false;
		_offscreenTravelSecondsRemaining = 0.0;
		StopMovement();

		if (!_routineStarted)
			return;

		if (_isWaiting)
		{
			RestoreCurrentTaskPresentation();
			return;
		}

		if (!_isTalkingToPlayer)
			ExecuteCurrentTask();
	}

	public void ResumeInInactiveScene()
	{
		if (_transitionStarted)
			return;

		_isInActiveScene = false;
		SetActiveScenePresentation(false);
		IsChangingScene = false;
		StopMovement();

		if (_routineStarted && !_isWaiting)
			ExecuteCurrentTask();
	}

	public void OnPlayerEnteredScene()
	{
		Node player =
			GetTree().GetFirstNodeInGroup("Player");

		if (
			_routineStarted
			|| !_isInActiveScene
			|| player == null
		)
			return;

		_routineStarted = true;
		GD.Print("Hubner: rotina iniciada após a entrada do jogador.");
		ExecuteCurrentTask();
	}

	public void MarkSceneTransitionCompleted()
	{
		_transitionStarted = false;
		_transitionSecondsRemaining = 0.0;
		_pendingDestinationScenePath = "";
		_pendingDestinationSpawnName = "";
	}

	private void GoToLocation(string locationName)
	{
		Node currentScene = GameManager.Instance.GetCurrentScene();
		LocationManager locationManager =
			currentScene?.GetNodeOrNull<LocationManager>(
				"LocationManager"
			);

		if (locationManager == null)
		{
			GD.PrintErr(
				"Hubner: LocationManager não encontrado na cena atual."
			);
			return;
		}

		if (
			!locationManager.TryGetLocation(
				locationName,
				out Vector2 target
			)
		)
		{
			GD.PrintErr(
				$"Hubner: destino '{locationName}' não encontrado."
			);
			return;
		}

		if (GlobalPosition.DistanceTo(target) <= ArrivalDistance)
		{
			CompleteCurrentTask();
			return;
		}

		_targetPosition = target;
		_hasTargetPosition = true;
		_movementAxis = MovementAxis.None;
		_agent.TargetPosition = target;
		_isExecutingTask = true;
	}

	private void StartOffscreenMovement(NPCTask task)
	{
		_offscreenTravelSecondsRemaining =
			task.Duration > 0.0f
				? task.Duration
				: DefaultOffscreenTravelSeconds;
		_isExecutingTask = true;
	}

	private void PrepareCurrentMovementForOffscreen()
	{
		if (_hasTargetPosition)
		{
			Vector2 remaining = _targetPosition - GlobalPosition;
			float cardinalDistance =
				Math.Abs(remaining.X) + Math.Abs(remaining.Y);

			_offscreenTravelSecondsRemaining = Math.Max(
				cardinalDistance / Math.Max(Speed, 1.0f),
				0.1f
			);
			return;
		}

		NPCTask task = NPCManager.Instance.GetCurrentTask(this);
		_offscreenTravelSecondsRemaining =
			task != null && task.Duration > 0.0f
				? task.Duration
				: DefaultOffscreenTravelSeconds;
	}

	private void StartWait(float duration)
	{
		if (_isWaiting)
			return;

		_isWaiting = true;
		_waitSecondsRemaining = Math.Max(duration, 0.1f);
		StopMovement();
		PlayCurrentTaskAnimation();
	}

	private void CompleteCurrentTask()
	{
		NPCTask task = NPCManager.Instance.GetCurrentTask(this);

		if (task == null)
			return;

		if (!_isInActiveScene && task.Type == NPCTask.TaskType.GoTo)
			NPCManager.Instance.RecordOffscreenArrival(this, task);

		_hasTargetPosition = false;
		_movementAxis = MovementAxis.None;

		if (task.ChangesScene())
		{
			BeginSceneTransition(
				task.DestinationScenePath,
				task.DestinationSpawnName
			);
			return;
		}

		AdvanceRoutine();
	}

	private void AdvanceRoutine()
	{
		NPCManager.Instance.GoToNextTask(this);
		ExecuteCurrentTask();
	}

	private void BeginSceneTransition(
		string destinationScenePath,
		string destinationSpawnName
	)
	{
		if (_transitionStarted)
			return;

		_transitionStarted = true;
		IsChangingScene = true;
		_isExecutingTask = false;
		StopMovement();
		_pendingDestinationScenePath = destinationScenePath;
		_pendingDestinationSpawnName = destinationSpawnName;
		_transitionSecondsRemaining = 0.75;
	}

	private Vector2 GetCardinalDirection(Vector2 offset)
	{
		bool hasHorizontal =
			Math.Abs(offset.X) > AxisAlignmentDistance;
		bool hasVertical =
			Math.Abs(offset.Y) > AxisAlignmentDistance;

		if (!hasHorizontal && !hasVertical)
			return Vector2.Zero;

		if (
			_movementAxis == MovementAxis.None
			|| (
				_movementAxis == MovementAxis.Horizontal
				&& !hasHorizontal
			)
			|| (
				_movementAxis == MovementAxis.Vertical
				&& !hasVertical
			)
		)
		{
			_movementAxis =
				hasHorizontal
				&& (
					!hasVertical
					|| Math.Abs(offset.X) >= Math.Abs(offset.Y)
				)
					? MovementAxis.Horizontal
					: MovementAxis.Vertical;
		}

		if (_movementAxis == MovementAxis.Horizontal)
			return offset.X > 0.0f ? Vector2.Right : Vector2.Left;

		return offset.Y > 0.0f ? Vector2.Down : Vector2.Up;
	}

	private void StopMovement()
	{
		Velocity = Vector2.Zero;
		_sprite?.Play("idle");
	}

	private void PauseForDialogue()
	{
		Velocity = Vector2.Zero;
		_sprite?.Pause();
	}

	private void RestoreCurrentTaskPresentation()
	{
		if (_isWaiting)
		{
			PlayCurrentTaskAnimation();
			return;
		}

		if (!_isExecutingTask)
			StopMovement();
	}

	private void PlayCurrentTaskAnimation()
	{
		if (_sprite == null)
			return;

		NPCTask task = NPCManager.Instance?.GetCurrentTask(this);

		if (
			task != null
			&& !string.IsNullOrEmpty(task.ActivityAnimation)
			&& _sprite.SpriteFrames.HasAnimation(
				task.ActivityAnimation
			)
		)
		{
			_sprite.Play(task.ActivityAnimation);
			return;
		}

		_sprite.Play("idle");
	}

	private void SetActiveScenePresentation(bool isActive)
	{
		Visible = isActive;
		CollisionLayer =
			isActive ? _activeCollisionLayer : 0;
		CollisionMask =
			isActive ? _activeCollisionMask : 0;

		if (_interactionArea == null)
			return;

		_interactionArea.Monitoring = isActive;
		_interactionArea.Monitorable = isActive;
		_interactionArea.CollisionLayer =
			isActive
				? _activeInteractionCollisionLayer
				: 0;
		_interactionArea.CollisionMask =
			isActive
				? _activeInteractionCollisionMask
				: 0;
	}

	private void Animate(Vector2 direction)
	{
		if (_sprite == null)
			return;

		if (direction.X != 0.0f)
		{
			_sprite.Play(direction.X > 0 ? "right" : "left");
			return;
		}

		_sprite.Play(direction.Y > 0 ? "down" : "up");
	}

	private void OnInteractionBodyEntered(Node body)
	{
		if (body.IsInGroup("Player"))
			_playerInRange = true;
	}

	private void OnInteractionBodyExited(Node body)
	{
		if (body.IsInGroup("Player"))
			_playerInRange = false;
	}

	public void OnReachedDoor(DoorArea door)
	{
		if (door == null || _transitionStarted)
			return;

		NPCTask task = NPCManager.Instance.GetCurrentTask(this);

		if (
			task == null
			|| !task.ChangesScene()
			|| task.DestinationScenePath
				!= door.DestinationScenePath
		)
		{
			return;
		}

		BeginSceneTransition(
			task.DestinationScenePath,
			task.DestinationSpawnName
		);
	}
}
