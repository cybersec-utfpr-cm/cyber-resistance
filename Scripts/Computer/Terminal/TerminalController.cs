using Godot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TelnetInterface;

public partial class TerminalController : Node
{
	[Signal]
	public delegate void OutputReceivedWithArgumentEventHandler(string output);

	[Signal]
	public delegate void ConnectionStartedEventHandler();

	[Signal]
	public delegate void ConnectionDelayedEventHandler();

	[Signal]
	public delegate void ConnectionSucceededEventHandler();

	[Signal]
	public delegate void ConnectionFailedEventHandler(string message);

	public string host { get; set; } = "127.0.0.1";
	public int port { get; set; } = 5000;
	public string username { get; set; } = "player";
	public string password { get; set; } = "player";
	public int loginTimeoutMs { get; set; } = 1000;
	public int maxAttempts { get; set; } = 10;
	public int retryDelayMs { get; set; } = 750;
	public int slowWarningAttempt { get; set; } = 4;
	public int totalTimeoutMs { get; set; } = 15000;

	private TelnetConnection telnet;
	private CancellationTokenSource sessionCts;
	private readonly CancellationTokenSource lifetimeCts = new();

	private Task connectionTask;
	private Task telnetTask;

	private readonly Channel<string> commandChannel =
		Channel.CreateUnbounded<string>(
			new UnboundedChannelOptions
			{
				SingleReader = true,
				SingleWriter = false
			}
		);

	private volatile bool isConnected;
	private volatile bool isConnecting;

	public void StartConnection()
	{
		if (isConnected || isConnecting)
		{
			Log.Info(
				"TerminalController: tentativa duplicada ignorada."
			);
			return;
		}

		isConnecting = true;
		connectionTask = ConnectWithRetry();
	}

	private async Task ConnectWithRetry()
	{
		CallDeferred(MethodName.EmitSignal, SignalName.ConnectionStarted);

		Log.Info($"TerminalController: conectando em {host}:{port}.");

		int attemptsLimit = Math.Max(1, maxAttempts);
		int retryDelay = Math.Max(0, retryDelayMs);
		int warningAttempt = Math.Clamp(
			slowWarningAttempt,
			1,
			attemptsLimit
		);

		using var timeoutCts =
			CancellationTokenSource.CreateLinkedTokenSource(
				lifetimeCts.Token
			);

		timeoutCts.CancelAfter(Math.Max(loginTimeoutMs, totalTimeoutMs));
		CancellationToken token = timeoutCts.Token;

		try
		{
			for (int attempt = 1; attempt <= attemptsLimit; attempt++)
			{
				TelnetConnection candidate = null;

				try
				{
					Log.Info(
						$"TerminalController: tentativa " +
						$"{attempt}/{attemptsLimit}."
					);

					candidate = await TelnetConnection.ConnectAsync(
						host,
						port,
						token
					);

					string loginOutput = await candidate.LoginAsync(
						username,
						password,
						loginTimeoutMs,
						token
					);

					token.ThrowIfCancellationRequested();

					sessionCts?.Cancel();
					sessionCts?.Dispose();
					sessionCts =
						CancellationTokenSource.CreateLinkedTokenSource(
							lifetimeCts.Token
						);

					telnet = candidate;
					candidate = null;
					isConnected = true;

					EmitOutput(loginOutput);

					telnetTask = RunTelnetAsync(
						telnet,
						sessionCts.Token
					);

					Log.Info(
						"TerminalController: conexão estabelecida."
					);

					CallDeferred(
						MethodName.EmitSignal,
						SignalName.ConnectionSucceeded
					);

					return;
				}
				catch (OperationCanceledException)
				{
					if (lifetimeCts.IsCancellationRequested)
						return;

					break;
				}
				catch (Exception exception)
				{
					Log.Error(
						$"TerminalController: tentativa " +
						$"{attempt} falhou: " +
						exception.Message
					);
				}
				finally
				{
					candidate?.Dispose();
				}

				if (attempt == warningAttempt)
				{
					CallDeferred(
						MethodName.EmitSignal,
						SignalName.ConnectionDelayed
					);
				}

				if (attempt < attemptsLimit)
					await Task.Delay(retryDelay, token);
			}

			if (!lifetimeCts.IsCancellationRequested)
			{
				EmitConnectionFailure(
					"Não foi possível iniciar o terminal. " +
					"Verifique o Docker e tente novamente."
				);
			}
		}
		catch (OperationCanceledException)
		{
			if (!lifetimeCts.IsCancellationRequested)
			{
				EmitConnectionFailure(
					"O terminal não respondeu dentro do " +
					"tempo esperado. Tente novamente."
				);
			}
		}
		finally
		{
			isConnecting = false;
			connectionTask = null;
		}
	}

	private async Task RunTelnetAsync(
		TelnetConnection connection,
		CancellationToken token
	)
	{
		using var stopCts =
			CancellationTokenSource.CreateLinkedTokenSource(token);

		Task readTask = ReadTelnetAsync(connection, stopCts.Token);
		Task writeTask = WriteTelnetAsync(connection, stopCts.Token);
		Exception disconnectException = null;

		try
		{
			Task completedTask = await Task.WhenAny(readTask, writeTask);
			await completedTask;
		}
		catch (OperationCanceledException)
			when (token.IsCancellationRequested)
		{
		}
		catch (Exception)
			when (token.IsCancellationRequested || lifetimeCts.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			isConnected = false;
			disconnectException = exception;

			Log.Error(
				$"TerminalController: conexão encerrada: " +
				exception.Message
			);
		}
		finally
		{
			isConnected = false;
			stopCts.Cancel();
			connection.Close();

			await IgnoreSessionEndAsync(readTask);
			await IgnoreSessionEndAsync(writeTask);

			connection.Dispose();

			if (ReferenceEquals(telnet, connection))
				telnet = null;
		}

		if (
			disconnectException != null &&
			!token.IsCancellationRequested &&
			!lifetimeCts.IsCancellationRequested
		)
		{
			EmitOutput("\nConexão encerrada.\n");

			EmitConnectionFailure(
				"A conexão com o terminal foi encerrada. " +
				"Tente novamente."
			);
		}
	}

	private async Task ReadTelnetAsync(
		TelnetConnection connection,
		CancellationToken token
	)
	{
		while (true)
		{
			string output = await connection.ReadAsync(token);

			if (output == null)
			{
				throw new IOException(
					"O servidor fechou a conexão Telnet."
				);
			}

			EmitOutput(output);
		}
	}

	private async Task WriteTelnetAsync(
		TelnetConnection connection,
		CancellationToken token
	)
	{
		while (await commandChannel.Reader.WaitToReadAsync(token))
		{
			while (commandChannel.Reader.TryRead(out string command))
			{
				Log.Info("TerminalController: comando enviado.");
				await connection.WriteLineAsync(command, token);
			}
		}
	}

	private static async Task IgnoreSessionEndAsync(Task task)
	{
		try
		{
			await task;
		}
		catch
		{
		}
	}

	private void EmitOutput(string output)
	{
		if (string.IsNullOrEmpty(output))
			return;

		CallDeferred(
			MethodName.EmitSignal,
			SignalName.OutputReceivedWithArgument,
			output
		);
	}

	private void EmitConnectionFailure(string message)
	{
		Log.Error($"TerminalController: falha definitiva. {message}");

		CallDeferred(
			MethodName.EmitSignal,
			SignalName.ConnectionFailed,
			message
		);
	}

	public void SendCommand(string command)
	{
		if (!isConnected)
		{
			Log.Error(
				"TerminalController: comando ignorado sem conexão."
			);
			return;
		}

		if (!commandChannel.Writer.TryWrite(command))
		{
			Log.Error(
				"TerminalController: não foi possível enfileirar o comando."
			);
		}
	}

	public override async void _ExitTree()
	{
		lifetimeCts.Cancel();
		sessionCts?.Cancel();
		commandChannel.Writer.TryComplete();
		telnet?.Close();

		Task activeConnectionTask = connectionTask;

		try
		{
			if (activeConnectionTask != null)
				await activeConnectionTask;

			Task activeTelnetTask = telnetTask;

			if (activeTelnetTask != null)
				await activeTelnetTask;
		}
		catch
		{
		}
		finally
		{
			telnet?.Dispose();
			telnet = null;
			sessionCts?.Dispose();
			lifetimeCts.Dispose();
		}
	}
}
