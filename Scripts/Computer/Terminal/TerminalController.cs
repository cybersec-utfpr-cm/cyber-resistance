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

	public string host { get; set; } = "127.0.0.1";
	public int port { get; set; } = 5000;
	public string username { get; set; } = "player";
	public string password { get; set; } = "player";
	public int loginTimeoutMs { get; set; } = 1000;
	public int maxAttempts { get; set; } = 15;
	public int retryDelayMs { get; set; } = 1000;

	private TelnetConnection telnet;
	private CancellationTokenSource cts;
	private Task telnetTask;
	private ConcurrentQueue<string> commandQueue = new();
	private bool isConnected = false;

	public void StartConnection()
	{
		if (isConnected || telnetTask != null)
			return;

		_ = ConnectWithRetry();
	}

	private async Task ConnectWithRetry()
	{
		Log.Info("Tentando conectar...");
		CallDeferred(MethodName.EmitSignal,
			SignalName.OutputReceivedWithArgument,
			"Ligando...");

		int attempt = 0;

		while (attempt < maxAttempts)
		{
			try
			{
				attempt++;
				Log.Info($"Tentativa {attempt}");

				telnet = new TelnetConnection(host, port);
				telnet.Login(username, password, loginTimeoutMs);

				Log.Info("Login deu certo");
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
				await Task.Delay(retryDelayMs);
			}
		}

		CallDeferred(MethodName.EmitSignal,
			SignalName.OutputReceivedWithArgument,
			" Falha ao conectar no ambiente Docker.\n");
		Log.Error("Nao conseguiu conectar apos varias tentativas.");
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
				CallDeferred(MethodName.EmitSignal,
					SignalName.OutputReceivedWithArgument,
					" Conexao encerrada.\n");
				break;
			}
		}
	}

	public void SendCommand(string command)
	{
		if (!isConnected)
			return;

		Log.Info("Comando enfileirado");
		commandQueue.Enqueue(command);
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
