using Godot;

public partial class Computer : Control
{
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("ui_cancel")) // Tecla ESC
		{
			GD.Print("Apertou esc");
			ExitComputer();
		}
	}

	public void ExitComputer()
	{
		// Despausa o jogo
		GetTree().Paused = false;

		// Remove a si mesmo da árvore
		QueueFree();

		GD.Print("Computer: Computador fechado.");
	}
}
