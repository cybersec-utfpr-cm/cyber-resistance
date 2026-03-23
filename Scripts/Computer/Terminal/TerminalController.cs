using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using MinimalisticTelnet;

public partial class TerminalController : Node
{
	[Signal]
	public delegate void OutputReceivedWithArgumentEventHandler(string output);

	private TelnetConnection telnet;
	private CancellationTokenSource cts;
	private Task telnetTask;
	private ConcurrentQueue<string> commandQueue = new();
	private bool isConnected = false;

	public override void _Ready()
	{
		_ = ConnectWithRetry();
	}

	private async Task ConnectWithRetry()
	{
		Log.Info("Tentando conectar...");
		// Envia o texto "Ligando..." à saída do terminal, bloqueando,
		// também, a entrada de texto até que haja conexão com a máquina Docker
		CallDeferred(MethodName.EmitSignal,
						SignalName.OutputReceivedWithArgument,
						"Ligando...");

		int maxAttempts = 10;
		int attempt = 0;

		while (attempt < maxAttempts)
		{
			try
			{
				attempt++;
				Log.Info($"Tentativa {attempt}");

				telnet = new TelnetConnection("127.0.0.1", 5000);

				telnet.Login("player", "player", 1000);

				Log.Info("Login deu certo :)");
				CallDeferred(MethodName.EmitSignal,
						SignalName.OutputReceivedWithArgument,
						" Tudo pronto!\n");
				isConnected = true;

				cts = new CancellationTokenSource();
				telnetTask = RunTelnetAsync(cts.Token);

				return;
			}
			catch (Exception e)
			{
				Log.Error($"Falhou tentativa {attempt}: {e.Message}");
				await Task.Delay(1000);
			}
		}

		Log.Error("Não conseguiu conectar após várias tentativas.");
	}

	private async Task RunTelnetAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				while (commandQueue.TryDequeue(out string cmd))
				{
					Log.Info("Comando enviado");
					telnet.WriteLine(cmd);
				}

				string output = telnet.Read();

				if (!string.IsNullOrEmpty(output))
				{
					CallDeferred(MethodName.EmitSignal,
						SignalName.OutputReceivedWithArgument,
						output);
				}

				await Task.Delay(10, token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception e)
			{
				Log.Error($"Erro telnet: {e.Message}");
				break;
			}
		}
	}

	public void SendCommand(string command)
	{
		if (isConnected)
		{
			Log.Info("Comando enfileirado");
			commandQueue.Enqueue(command);
		}
	}

	public override async void _ExitTree()
	{
		if (cts != null)
		{
			cts.Cancel();

			try
			{
				await telnetTask;
			}
			catch { }

			cts.Dispose();
		}
	}
}