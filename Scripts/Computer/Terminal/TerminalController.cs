using Godot;
using System;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TelnetInterface;

public partial class TerminalController : Node
{
	[Signal]
	public delegate void OutputReceivedEventHandler(byte[] output);

	[Signal]
	public delegate void ConnectionStartedEventHandler();

	[Signal]
	public delegate void ConnectionDelayedEventHandler();

	[Signal]
	public delegate void ConnectionSucceededEventHandler();

	[Signal]
	public delegate void ConnectionFailedEventHandler(string message);

	[Signal]
	public delegate void ConnectionClosedEventHandler();

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

	private readonly record struct TerminalInput(byte[] Data, int Columns, int Rows);

	private Channel<TerminalInput> _inputChannel;
	private int _terminalColumns = 80;
	private int _terminalRows = 24;

	private volatile bool isConnected;
	private volatile bool isConnecting;
	public bool HasActiveSession => isConnected;

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
		_inputChannel = Channel.CreateUnbounded<TerminalInput>(
			new UnboundedChannelOptions { SingleReader = true }
		);
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
					await candidate.SetWindowSizeAsync(
						_terminalColumns,
						_terminalRows,
						token
					);

					string loginOutput = await candidate.LoginAsync(
						username,
						password,
						loginTimeoutMs,
						token
					);
					await candidate.SetWindowSizeAsync(
						_terminalColumns,
						_terminalRows,
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

					CallDeferred(
						MethodName.EmitSignal,
						SignalName.ConnectionSucceeded
					);
					EmitOutput(Encoding.UTF8.GetBytes(loginOutput));

					telnetTask = RunTelnetAsync(
						telnet,
						_inputChannel,
						sessionCts.Token
					);

					Log.Info(
						"TerminalController: conexão estabelecida."
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
		Channel<TerminalInput> inputChannel,
		CancellationToken token
	)
	{
		using var stopCts =
			CancellationTokenSource.CreateLinkedTokenSource(token);

		Task readTask = ReadTelnetAsync(connection, stopCts.Token);
		Task writeTask = WriteTelnetAsync(connection, inputChannel, stopCts.Token);
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
			inputChannel.Writer.TryComplete();
			stopCts.Cancel();
			connection.Close();

			await IgnoreSessionEndAsync(readTask);
			await IgnoreSessionEndAsync(writeTask);

			connection.Dispose();

			if (ReferenceEquals(telnet, connection))
				telnet = null;
		}

		if (token.IsCancellationRequested || lifetimeCts.IsCancellationRequested)
			return;

		if (disconnectException != null)
		{
			EmitConnectionFailure(
				"A conexão com o terminal foi encerrada. " +
				"Tente novamente."
			);
		}
		else
		{
			CallDeferred(MethodName.EmitSignal, SignalName.ConnectionClosed);
		}
	}

	private async Task ReadTelnetAsync(
		TelnetConnection connection,
		CancellationToken token
	)
	{
		while (true)
		{
			byte[] output = await connection.ReadDataAsync(token);

			if (output == null)
				return;

			EmitOutput(output);
		}
	}

	private async Task WriteTelnetAsync(
		TelnetConnection connection,
		Channel<TerminalInput> inputChannel,
		CancellationToken token
	)
	{
		while (await inputChannel.Reader.WaitToReadAsync(token))
		{
			while (inputChannel.Reader.TryRead(out TerminalInput input))
			{
				if (input.Data != null)
					await connection.WriteAsync(input.Data, token);
				else
					await connection.SetWindowSizeAsync(input.Columns, input.Rows, token);
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

	private void EmitOutput(byte[] output)
	{
		if (output.Length == 0)
			return;

		CallDeferred(
			MethodName.EmitSignal,
			SignalName.OutputReceived,
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

	public void SendInput(byte[] data)
	{
		if (!isConnected || data.Length == 0)
			return;

		if (!_inputChannel.Writer.TryWrite(new TerminalInput(data, 0, 0)))
		{
			Log.Error(
				"TerminalController: não foi possível enfileirar a entrada."
			);
		}
	}

	public void ResizeTerminal(int columns, int rows)
	{
		if (columns < 1 || rows < 1)
			return;

		_terminalColumns = Math.Clamp(columns, 1, ushort.MaxValue);
		_terminalRows = Math.Clamp(rows, 1, ushort.MaxValue);

		if (isConnected)
		{
			_inputChannel.Writer.TryWrite(
				new TerminalInput(null, _terminalColumns, _terminalRows)
			);
		}
	}

	public override async void _ExitTree()
	{
		lifetimeCts.Cancel();
		sessionCts?.Cancel();
		_inputChannel?.Writer.TryComplete();
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
