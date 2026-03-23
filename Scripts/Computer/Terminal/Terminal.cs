using Godot;
using System;

public partial class Terminal : Control
{
	private DockerManager docker;

	public override async void _Ready()
	{
		base._Ready();
		Log.Info("Entrou na cena do terminal");

		docker = new DockerManager("player_machine");

		try
		{
			await docker.StartAsync();
		}
		catch (Exception e)
		{
			Log.Error($"Falha ao iniciar container Docker: {e.Message}");
		}
	}

	public override async void _ExitTree()
	{
		if (docker != null)
		{
			try
			{
				await docker.StopAsync();
			}
			catch (Exception e)
			{
				Log.Error($"Falha ao parar container Docker: {e.Message}");
			}
			finally
			{
				docker.Dispose();
			}
		}

		base._ExitTree();
	}

	private void _on_back_icon_bt_pressed()
	{
		GetParent<Screens>().ShowScreen("Desktop");
	}
}
