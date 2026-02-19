using Godot;
using System;
using System.Collections.Generic;


public partial class Screens : Control {
	
	private Dictionary<string, Control> _screens = new();
	public override void _Ready()
	{
		// pega automaticamente todas as telas dentro de Screens
		foreach (Node child in GetChildren())
		{
			if (child is Control control)
			{
				_screens[control.Name] = control;
			}
		}

		// começa mostrando o Desktop
		ShowScreen("Desktop");
	}

	public void ShowScreen(string screenName)
	{
		// esconde todas
		foreach (var screen in _screens.Values)
			screen.Visible = false;

		// mostra a escolhida
		if (_screens.ContainsKey(screenName))
			_screens[screenName].Visible = true;
		else
			GD.Print("Tela não encontrada: " + screenName);
	}
}
