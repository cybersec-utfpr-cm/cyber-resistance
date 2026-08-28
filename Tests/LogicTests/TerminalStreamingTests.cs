using System.Net;
using System.Net.Sockets;
using System.Text;
using TelnetInterface;

public static class TerminalStreamingTests
{
	private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

	public static async Task DeliversInitialPromptAsync()
	{
		TaskCompletionSource<bool> releaseServer = NewSignal();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteAsync(
					stream,
					new byte[]
					{
						255, 253, 3,
						255, 251, 1
					},
					token
				);
				await WriteTextAsync(
					stream,
					"Debian GNU/Linux\r\nlogin: ",
					token
				);

				byte[] response = await ReadExactlyAsync(stream, 6, token);
				SequenceEqual(
					new byte[]
					{
						255, 251, 3,
						255, 253, 1
					},
					response,
					"As negociações SGA/ECHO não foram aceitas."
				);

				Equal("player", await ReadLineAsync(stream, token));
				await WriteTextAsync(stream, "Password: ", token);
				Equal("player", await ReadLineAsync(stream, token));
				await WriteTextAsync(
					stream,
					"Bem-vindo\r\nplayer@machine:~$ ",
					token
				);

				await releaseServer.Task.WaitAsync(token);
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		try
		{
			string loginOutput = await connection.LoginAsync(
				"player",
				"player",
				2000,
				testCts.Token
			);

			Contains("login: ", loginOutput);
			Contains("Password: ", loginOutput);
			Contains("player@machine:~$ ", loginOutput);
		}
		finally
		{
			releaseServer.TrySetResult(true);
		}

		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task DeliversFragmentsBeforeCommandEndsAsync()
	{
		TaskCompletionSource<bool> firstSent = NewSignal();
		TaskCompletionSource<bool> sendSecond = NewSignal();
		TaskCompletionSource<bool> secondSent = NewSignal();
		TaskCompletionSource<bool> finishCommand = NewSignal();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				Equal("comando-longo", await ReadLineAsync(stream, token));
				await WriteTextAsync(stream, "primeiro fragmento\n", token);
				firstSent.TrySetResult(true);

				await sendSecond.Task.WaitAsync(token);
				await WriteTextAsync(stream, "segundo fragmento\n", token);
				secondSent.TrySetResult(true);

				await finishCommand.Task.WaitAsync(token);
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		try
		{
			await connection.WriteLineAsync("comando-longo", testCts.Token);
			await firstSent.Task.WaitAsync(testCts.Token);

			string first = await connection.ReadAsync(testCts.Token);
			Equal("primeiro fragmento\n", first);
			True(
				!server.Completion.IsCompleted,
				"O primeiro fragmento só chegou após o comando terminar."
			);

			sendSecond.TrySetResult(true);
			await secondSent.Task.WaitAsync(testCts.Token);

			string second = await connection.ReadAsync(testCts.Token);
			Equal("segundo fragmento\n", second);
			True(
				!server.Completion.IsCompleted,
				"O segundo fragmento só chegou após o comando terminar."
			);
		}
		finally
		{
			sendSecond.TrySetResult(true);
			finishCommand.TrySetResult(true);
		}

		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task DoesNotWaitForPauseAsync()
	{
		TaskCompletionSource<bool> producerStarted = NewSignal();
		TaskCompletionSource<bool> stopProducer = NewSignal();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteTextAsync(stream, "0", token);
				producerStarted.TrySetResult(true);

				while (!stopProducer.Task.IsCompleted)
				{
					await Task.Delay(20, token);
					await WriteTextAsync(stream, "x", token);
				}
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		try
		{
			await producerStarted.Task.WaitAsync(testCts.Token);

			string output = await CompleteWithinAsync(
				connection.ReadAsync(testCts.Token),
				TimeSpan.FromMilliseconds(500),
				"A leitura aguardou uma pausa mesmo com dados disponíveis."
			);

			True(output.Length > 0, "A leitura imediata não entregou dados.");
			True(
				!server.Completion.IsCompleted,
				"O produtor contínuo terminou antes da primeira entrega."
			);
		}
		finally
		{
			stopProducer.TrySetResult(true);
		}

		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task SanitizesSplitSequencesAsync()
	{
		byte[][] fragments =
		{
			Encoding.UTF8.GetBytes("\x1B[3"),
			Combine(Encoding.UTF8.GetBytes("1mA"), new byte[] { 0xC3 }),
			Combine(new byte[] { 0xA7 }, Encoding.UTF8.GetBytes("\x1B]0;tít")),
			Encoding.UTF8.GetBytes("ulo\x07\x1B[0m10%\r"),
			Encoding.UTF8.GetBytes("20%\r\nfim"),
			Encoding.UTF8.GetBytes("]300"),
			Encoding.UTF8.GetBytes("8;shell metadata"),
			Encoding.UTF8.GetBytes("\\ok")
		};

		TaskCompletionSource<bool>[] fragmentSent =
			fragments.Select(_ => NewSignal()).ToArray();
		TaskCompletionSource<bool>[] fragmentConsumed =
			fragments.Select(_ => NewSignal()).ToArray();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				for (int index = 0; index < fragments.Length; index++)
				{
					await WriteAsync(stream, fragments[index], token);
					fragmentSent[index].TrySetResult(true);
					await fragmentConsumed[index].Task.WaitAsync(token);
				}
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);
		var sanitizer = new TerminalOutputSanitizer();
		var displayed = new StringBuilder();

		for (int index = 0; index < fragments.Length; index++)
		{
			await fragmentSent[index].Task.WaitAsync(testCts.Token);
			string fragment = await connection.ReadAsync(testCts.Token);
			displayed.Append(sanitizer.Process(fragment));
			fragmentConsumed[index].TrySetResult(true);
		}

		await server.Completion.WaitAsync(testCts.Token);

		Equal("Aç10%\n20%\nfimok", displayed.ToString());
		DoesNotContain("[31", displayed.ToString());
		DoesNotContain("título", displayed.ToString());
		DoesNotContain("�", displayed.ToString());
	}

	public static async Task CancelsPendingReadAsync()
	{
		TaskCompletionSource<bool> accepted = NewSignal();
		TaskCompletionSource<bool> releaseServer = NewSignal();

		await using var server = new LoopbackServer(
			async (_, token) =>
			{
				accepted.TrySetResult(true);
				await releaseServer.Task.WaitAsync(token);
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		await accepted.Task.WaitAsync(testCts.Token);

		using var readCts = new CancellationTokenSource();
		Task<string> readTask = connection.ReadAsync(readCts.Token);
		readCts.Cancel();

		try
		{
			await ThrowsWithinAsync<OperationCanceledException>(
				readTask,
				TimeSpan.FromSeconds(1),
				"A leitura pendente não foi cancelada."
			);
		}
		finally
		{
			releaseServer.TrySetResult(true);
		}

		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task ReportsDisconnectAsync()
	{
		TaskCompletionSource<bool> accepted = NewSignal();

		await using var server = new LoopbackServer(
			(_, _) =>
			{
				accepted.TrySetResult(true);
				return Task.CompletedTask;
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		await accepted.Task.WaitAsync(testCts.Token);
		string output = await CompleteWithinAsync(
			connection.ReadAsync(testCts.Token),
			TimeSpan.FromSeconds(1),
			"A desconexão não foi reportada."
		);

		True(output == null, "EOF não foi reportado como desconexão.");
		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task KeepsReadAndWriteIndependentAsync()
	{
		TaskCompletionSource<bool> accepted = NewSignal();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				accepted.TrySetResult(true);
				Equal("status", await ReadLineAsync(stream, token));
				await WriteTextAsync(stream, "ok\n", token);
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		await accepted.Task.WaitAsync(testCts.Token);

		Task<string> readTask = connection.ReadAsync(testCts.Token);
		await connection.WriteLineAsync("status", testCts.Token);
		Equal("ok\n", await readTask.WaitAsync(testCts.Token));
		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task PreservesSplitTelnetNegotiationAsync()
	{
		byte[][] fragments =
		{
			new byte[] { 255 },
			new byte[] { 253 },
			new byte[] { 3, (byte)'o', (byte)'k' }
		};

		TaskCompletionSource<bool>[] fragmentSent =
			fragments.Select(_ => NewSignal()).ToArray();
		TaskCompletionSource<bool>[] fragmentConsumed =
			fragments.Select(_ => NewSignal()).ToArray();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				for (int index = 0; index < fragments.Length; index++)
				{
					await WriteAsync(stream, fragments[index], token);
					fragmentSent[index].TrySetResult(true);
					await fragmentConsumed[index].Task.WaitAsync(token);
				}

				byte[] response = await ReadExactlyAsync(stream, 3, token);
				SequenceEqual(
					new byte[] { 255, 251, 3 },
					response,
					"A negociação Telnet dividida perdeu estado."
				);
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);

		await fragmentSent[0].Task.WaitAsync(testCts.Token);
		Equal(string.Empty, await connection.ReadAsync(testCts.Token));
		fragmentConsumed[0].TrySetResult(true);

		await fragmentSent[1].Task.WaitAsync(testCts.Token);
		Equal(string.Empty, await connection.ReadAsync(testCts.Token));
		fragmentConsumed[1].TrySetResult(true);

		await fragmentSent[2].Task.WaitAsync(testCts.Token);
		Equal("ok", await connection.ReadAsync(testCts.Token));
		fragmentConsumed[2].TrySetResult(true);

		await server.Completion.WaitAsync(testCts.Token);
	}

	public static async Task UsesServerControlledEchoAsync()
	{
		TaskCompletionSource<bool> negotiationSent = NewSignal();
		TaskCompletionSource<bool> commandOutputSent = NewSignal();
		TaskCompletionSource<bool> authenticatedOutputSent = NewSignal();

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteAsync(
					stream,
					new byte[] { 255, 251, 1 },
					token
				);
				negotiationSent.TrySetResult(true);

				byte[] response = await ReadExactlyAsync(stream, 3, token);
				SequenceEqual(
					new byte[] { 255, 253, 1 },
					response,
					"O cliente não aceitou o eco controlado pelo servidor."
				);

				Equal(
					"ssh bob@172.17.0.3",
					await ReadLineAsync(stream, token)
				);
				await WriteTextAsync(
					stream,
					"ssh bob@172.17.0.3\r\nPassword: ",
					token
				);
				commandOutputSent.TrySetResult(true);

				Equal("password", await ReadLineAsync(stream, token));
				await WriteTextAsync(
					stream,
					"\r\nAutenticado\r\nbob@target:~$ ",
					token
				);
				authenticatedOutputSent.TrySetResult(true);
			}
		);

		using var testCts = new CancellationTokenSource(TestTimeout);
		using TelnetConnection connection = await TelnetConnection.ConnectAsync(
			IPAddress.Loopback.ToString(),
			server.Port,
			testCts.Token
		);
		var sanitizer = new TerminalOutputSanitizer();

		await negotiationSent.Task.WaitAsync(testCts.Token);
		Equal(string.Empty, await connection.ReadAsync(testCts.Token));

		await connection.WriteLineAsync(
			"ssh bob@172.17.0.3",
			testCts.Token
		);
		await commandOutputSent.Task.WaitAsync(testCts.Token);
		string commandOutput = sanitizer.Process(
			await connection.ReadAsync(testCts.Token)
		);
		Contains("ssh bob@172.17.0.3\nPassword: ", commandOutput);

		await connection.WriteLineAsync("password", testCts.Token);
		await authenticatedOutputSent.Task.WaitAsync(testCts.Token);
		string authenticatedOutput = sanitizer.Process(
			await connection.ReadAsync(testCts.Token)
		);
		Contains("Autenticado\nbob@target:~$ ", authenticatedOutput);
		DoesNotContain("password", authenticatedOutput);

		await server.Completion.WaitAsync(testCts.Token);
	}

	private static async Task<T> CompleteWithinAsync<T>(
		Task<T> task,
		TimeSpan timeout,
		string failureMessage
	)
	{
		Task completed = await Task.WhenAny(task, Task.Delay(timeout));

		if (!ReferenceEquals(completed, task))
			throw new InvalidOperationException(failureMessage);

		return await task;
	}

	private static async Task ThrowsWithinAsync<TException>(
		Task task,
		TimeSpan timeout,
		string failureMessage
	)
		where TException : Exception
	{
		try
		{
			await CompleteWithinAsync(
				AwaitAndReturnTrueAsync(task),
				timeout,
				failureMessage
			);
		}
		catch (TException)
		{
			return;
		}

		throw new InvalidOperationException(
			$"Era esperada a exceção {typeof(TException).Name}."
		);
	}

	private static async Task<bool> AwaitAndReturnTrueAsync(Task task)
	{
		await task;
		return true;
	}

	private static async Task<string> ReadLineAsync(
		NetworkStream stream,
		CancellationToken cancellationToken
	)
	{
		var bytes = new List<byte>();
		byte[] buffer = new byte[1];

		while (true)
		{
			int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

			if (bytesRead == 0)
				throw new IOException("O cliente desconectou antes do fim da linha.");

			if (buffer[0] == (byte)'\n')
				break;

			if (buffer[0] != (byte)'\r')
				bytes.Add(buffer[0]);
		}

		return Encoding.UTF8.GetString(bytes.ToArray());
	}

	private static async Task<byte[]> ReadExactlyAsync(
		NetworkStream stream,
		int count,
		CancellationToken cancellationToken
	)
	{
		byte[] bytes = new byte[count];
		int offset = 0;

		while (offset < count)
		{
			int bytesRead = await stream.ReadAsync(
				bytes.AsMemory(offset, count - offset),
				cancellationToken
			);

			if (bytesRead == 0)
				throw new IOException("O cliente desconectou durante a leitura.");

			offset += bytesRead;
		}

		return bytes;
	}

	private static async Task WriteTextAsync(
		NetworkStream stream,
		string value,
		CancellationToken cancellationToken
	)
	{
		await WriteAsync(
			stream,
			Encoding.UTF8.GetBytes(value),
			cancellationToken
		);
	}

	private static async Task WriteAsync(
		NetworkStream stream,
		byte[] bytes,
		CancellationToken cancellationToken
	)
	{
		await stream.WriteAsync(bytes, cancellationToken);
	}

	private static byte[] Combine(byte[] first, byte[] second)
	{
		byte[] combined = new byte[first.Length + second.Length];
		Buffer.BlockCopy(first, 0, combined, 0, first.Length);
		Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
		return combined;
	}

	private static TaskCompletionSource<bool> NewSignal()
	{
		return new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
	}

	private static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException(
				$"Esperado: '{expected}'. Obtido: '{actual}'."
			);
		}
	}

	private static void Contains(string expected, string actual)
	{
		if (!actual.Contains(expected, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Trecho esperado não encontrado: {expected}\nConteúdo:\n{actual}"
			);
		}
	}

	private static void DoesNotContain(string unexpected, string actual)
	{
		if (actual.Contains(unexpected, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Trecho inesperado encontrado: {unexpected}\nConteúdo:\n{actual}"
			);
		}
	}

	private static void SequenceEqual(
		byte[] expected,
		byte[] actual,
		string message
	)
	{
		if (!expected.SequenceEqual(actual))
			throw new InvalidOperationException(message);
	}

	private static void True(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	private sealed class LoopbackServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly CancellationTokenSource _lifetimeCts = new(TestTimeout);

		public LoopbackServer(
			Func<NetworkStream, CancellationToken, Task> handler
		)
		{
			_listener = new TcpListener(IPAddress.Loopback, 0);
			_listener.Start();
			Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
			Completion = AcceptAndHandleAsync(handler);
		}

		public int Port { get; }

		public Task Completion { get; }

		public async ValueTask DisposeAsync()
		{
			_lifetimeCts.Cancel();
			_listener.Stop();

			try
			{
				await Completion;
			}
			catch
			{
			}

			_lifetimeCts.Dispose();
		}

		private async Task AcceptAndHandleAsync(
			Func<NetworkStream, CancellationToken, Task> handler
		)
		{
			using TcpClient client = await _listener.AcceptTcpClientAsync(
				_lifetimeCts.Token
			);
			client.NoDelay = true;
			await handler(client.GetStream(), _lifetimeCts.Token);
		}
	}
}
