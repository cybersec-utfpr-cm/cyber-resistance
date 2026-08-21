// Script responsável pelo gerenciamento da movimentação do player. 

using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerMovement : CharacterBody2D
{
	// === Configurações ===
	[Export] public float Speed { get; set; } = 90.0f; // utilizando da velocidade ajustável do editor;
	private Vector2 _inputDirection = Vector2.Zero;
	
	// === Estados === 
	private bool _isMoving = false; 
	private double _footstepCooldown;
	private const double FootstepInterval = 0.32;
	
	// === Referências ===
	private AnimatedSprite2D _sprite;
	
	// === Animações ===
	private Dictionary<Vector2, string> _animationByDirection = new() {
		{Vector2.Left, "left"},
		{Vector2.Right, "right"},
		{Vector2.Up, "up"},
		{Vector2.Down, "down"}
	};
	
	public override void _Ready() {
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
	}

	public override void _PhysicsProcess(double delta) {
		_inputDirection = InputDirection(); // input do jogador;
		_isMoving = _inputDirection != Vector2.Zero; // está se movendo?
		if(_isMoving) { // se está se movendo... 
			Velocity = _inputDirection.Normalized() * Speed;
			MoveAndSlide();
			AnimatePlayer(_inputDirection);
			UpdateFootsteps(delta);
		}
		else {
			Velocity = Vector2.Zero;
			_footstepCooldown = 0.0;
			StopAnimation();
		}
	}

	private void UpdateFootsteps(double delta)
	{
		_footstepCooldown -= delta;

		if (_footstepCooldown > 0.0)
			return;

		AudioManager.Instance?.PlayFootstep();
		_footstepCooldown = FootstepInterval;
	}
	// === DIREÇÃO DO INPUT ===
	private Vector2 InputDirection()
	{
		float x = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		float y = Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up");
		return new Vector2(x, y);
	}

	// === ANIMAÇÕES ===
	private void AnimatePlayer(Vector2 direction)
	{
		// Normaliza direção para pegar as animações corretas
		Vector2 dir = Vector2.Zero;

		if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
			dir = direction.X > 0 ? Vector2.Right : Vector2.Left;
		else if (direction.Y != 0)
			dir = direction.Y > 0 ? Vector2.Down : Vector2.Up;

		if (_animationByDirection.TryGetValue(dir, out string anim))
		{
			if (_sprite.Animation != anim)
				_sprite.Play(anim);
		}
	}

	private void StopAnimation()
	{
		_sprite.Frame = 1;
	}
	
	//public override void _Input(InputEvent @event)
	//{
		//if (@event.IsActionPressed("ui_accept")) // tecla Enter, por exemplo
		//{
			//QuestManager.Instance.SetQuestStage("phishing", 2);
		//}
	//}
}
