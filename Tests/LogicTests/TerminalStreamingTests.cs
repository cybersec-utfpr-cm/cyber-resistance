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

	public static async Task RecognizesStyledLoginPromptAsync()
	{
		const string shell = "\u001b]0;player@machine\u0007\u001b[32mplayer@machine:~$ \u001b[0m\u001b[?2004h";
		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteTextAsync(stream, "\u001b[32mlogin:\u001b[0m ", token);
				Equal("player", await ReadLineAsync(stream, token));
				await WriteTextAsync(stream, "\u001b[32mPassword:\u001b[0m ", token);
				Equal("player", await ReadLineAsync(stream, token));
				await WriteTextAsync(stream, shell, token);
			}
		);
		using var cts = new CancellationTokenSource(TestTimeout);
		using var connection = await TelnetConnection.ConnectAsync(
			"127.0.0.1", server.Port, cts.Token
		);

		string output = await connection.LoginAsync("player", "player", 2000, cts.Token);
		Contains(shell, output);
		await server.Completion.WaitAsync(cts.Token);
	}

	public static async Task WritesInteractiveBytesAsync()
	{
		byte[] expected = Combine(
			Encoding.UTF8.GetBytes("ação"),
			new byte[] { 27, 91, 65, 3, 4, 9, 127, 13, 255, 255, 26 }
		);
		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				SequenceEqual(
					expected,
					await ReadExactlyAsync(stream, expected.Length, token),
					"A entrada ganhou uma quebra de linha ou perdeu bytes de controle."
				);
			}
		);
		using var cts = new CancellationTokenSource(TestTimeout);
		using var connection = await TelnetConnection.ConnectAsync(
			"127.0.0.1", server.Port, cts.Token
		);

		await connection.WriteAsync("ação", cts.Token);
		await connection.WriteAsync(
			new byte[] { 27, 91, 65, 3, 4, 9, 127, 13, 255 }, cts.Token
		);
		await connection.WriteAsync(new byte[] { 26 }, cts.Token);
		await server.Completion.WaitAsync(cts.Token);
	}

	public static async Task PreservesScreenControlBytesAsync()
	{
		byte[] controls = Encoding.UTF8.GetBytes(
			"\u001b[?1049h\u001b[2J\u001b[3;8HAção\r\b\u001b[6n\u001b[?1049l"
		);
		byte[] expected = Combine(controls, new byte[] { 255 });
		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteAsync(stream, Combine(controls, new byte[] { 255, 255 }), token);
			}
		);
		using var cts = new CancellationTokenSource(TestTimeout);
		using var connection = await TelnetConnection.ConnectAsync(
			"127.0.0.1", server.Port, cts.Token
		);
		var received = new List<byte>();

		while (true)
		{
			byte[] data = await connection.ReadDataAsync(cts.Token);
			if (data == null)
				break;
			received.AddRange(data);
		}

		SequenceEqual(expected, received.ToArray(), "Os controles de tela foram alterados.");
		await server.Completion.WaitAsync(cts.Token);
	}

	public static async Task NegotiatesTerminalTypeAsync()
	{
		byte[][] fragments =
		{
			new byte[] { 255, 250, 24 },
			new byte[] { 1, 255 },
			new byte[] { 240 }
		};
		var sent = fragments.Select(_ => NewSignal()).ToArray();
		var consumed = fragments.Select(_ => NewSignal()).ToArray();
		byte[] terminalType = Combine(
			new byte[] { 255, 250, 24, 0 },
			Combine(Encoding.ASCII.GetBytes("xterm-256color"), new byte[] { 255, 240 })
		);

		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteAsync(stream, new byte[] { 255, 253, 24, (byte)'a' }, token);
				SequenceEqual(
					new byte[] { 255, 251, 24 },
					await ReadExactlyAsync(stream, 3, token),
					"O cliente não aceitou TERMINAL-TYPE."
				);
				for (int index = 0; index < fragments.Length; index++)
				{
					await WriteAsync(stream, fragments[index], token);
					sent[index].TrySetResult(true);
					await consumed[index].Task.WaitAsync(token);
				}
				SequenceEqual(
					terminalType,
					await ReadExactlyAsync(stream, terminalType.Length, token),
					"O tipo de terminal não foi informado após o SEND fragmentado."
				);
				await WriteAsync(
					stream,
					new byte[] { 255, 253, 24, 255, 250, 24, 1, 255, 240, (byte)'b' },
					token
				);
				SequenceEqual(
					terminalType,
					await ReadExactlyAsync(stream, terminalType.Length, token),
					"A negociação repetida gerou um ciclo ou mudou o tipo de terminal."
				);
			}
		);
		using var cts = new CancellationTokenSource(TestTimeout);
		using var connection = await TelnetConnection.ConnectAsync(
			"127.0.0.1", server.Port, cts.Token
		);

		await ReadMarkerAsync(connection, "a", cts.Token);
		for (int index = 0; index < fragments.Length; index++)
		{
			await sent[index].Task.WaitAsync(cts.Token);
			Equal(string.Empty, await connection.ReadAsync(cts.Token));
			consumed[index].TrySetResult(true);
		}
		await ReadMarkerAsync(connection, "b", cts.Token);
		await server.Completion.WaitAsync(cts.Token);
	}

	public static async Task NegotiatesWindowSizeAsync()
	{
		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteAsync(stream, new byte[] { 255, 253, 31, (byte)'a' }, token);
				SequenceEqual(
					new byte[] { 255, 251, 31, 255, 250, 31, 0, 120, 0, 40, 255, 240 },
					await ReadExactlyAsync(stream, 12, token),
					"O tamanho inicial da tela não chegou na negociação NAWS."
				);
				SequenceEqual(
					new byte[] { 255, 250, 31, 0, 255, 255, 1, 255, 255, 255, 240 },
					await ReadExactlyAsync(stream, 11, token),
					"O redimensionamento não escapou os bytes IAC."
				);
				await WriteAsync(stream, new byte[] { 255, 254, 31, (byte)'b' }, token);
				SequenceEqual(
					new byte[] { 255, 252, 31, (byte)'z' },
					await ReadExactlyAsync(stream, 4, token),
					"O cliente continuou enviando NAWS após DONT."
				);
				await WriteAsync(stream, new byte[] { 255, 253, 31, (byte)'c' }, token);
				SequenceEqual(
					new byte[] { 255, 251, 31, 255, 250, 31, 0, 80, 0, 24, 255, 240 },
					await ReadExactlyAsync(stream, 12, token),
					"O cliente não retomou NAWS com o tamanho mais recente."
				);
				await WriteAsync(stream, new byte[] { 255, 253, 31, (byte)'d' }, token);
				Equal((byte)'q', (await ReadExactlyAsync(stream, 1, token))[0]);
			}
		);
		using var cts = new CancellationTokenSource(TestTimeout);
		using var connection = await TelnetConnection.ConnectAsync(
			"127.0.0.1", server.Port, cts.Token
		);

		await connection.SetWindowSizeAsync(120, 40, cts.Token);
		await ReadMarkerAsync(connection, "a", cts.Token);
		await connection.SetWindowSizeAsync(255, 511, cts.Token);
		await ReadMarkerAsync(connection, "b", cts.Token);
		await connection.SetWindowSizeAsync(80, 24, cts.Token);
		await connection.WriteAsync("z", cts.Token);
		await ReadMarkerAsync(connection, "c", cts.Token);
		await ReadMarkerAsync(connection, "d", cts.Token);
		await connection.WriteAsync("q", cts.Token);
		await server.Completion.WaitAsync(cts.Token);
	}

	public static async Task NegotiatesBinaryWithoutEchoLoopAsync()
	{
		byte[] negotiation =
		{
			255, 253, 0, 255, 251, 0,
			255, 253, 99, 255, 251, 99,
			255, 251, 1, 255, 253, 1, (byte)'a'
		};
		byte[] expected =
		{
			255, 251, 0, 255, 253, 0,
			255, 252, 99, 255, 254, 99,
			255, 253, 1, 255, 252, 1
		};
		await using var server = new LoopbackServer(
			async (stream, token) =>
			{
				await WriteAsync(stream, negotiation, token);
				SequenceEqual(
					expected,
					await ReadExactlyAsync(stream, expected.Length, token),
					"BINARY, ECHO ou opções não suportadas foram negociados incorretamente."
				);
				await WriteAsync(
					stream, new byte[] { 255, 253, 0, 255, 251, 0, (byte)'b' }, token
				);
				Equal((byte)'x', (await ReadExactlyAsync(stream, 1, token))[0]);
				await WriteAsync(
					stream, new byte[] { 255, 254, 0, 255, 252, 0, 255, 252, 1, (byte)'c' }, token
				);
				SequenceEqual(
					new byte[] { 255, 252, 0, 255, 254, 0, 255, 254, 1 },
					await ReadExactlyAsync(stream, 9, token),
					"O encerramento das opções não foi reconhecido."
				);
			}
		);
		using var cts = new CancellationTokenSource(TestTimeout);
		using var connection = await TelnetConnection.ConnectAsync(
			"127.0.0.1", server.Port, cts.Token
		);

		await ReadMarkerAsync(connection, "a", cts.Token);
		await ReadMarkerAsync(connection, "b", cts.Token);
		await connection.WriteAsync("x", cts.Token);
		await ReadMarkerAsync(connection, "c", cts.Token);
		await server.Completion.WaitAsync(cts.Token);
	}

	private static async Task ReadMarkerAsync(
		TelnetConnection connection,
		string marker,
		CancellationToken token
	)
	{
		var output = new StringBuilder();
		while (!output.ToString().Contains(marker, StringComparison.Ordinal))
		{
			string fragment = await connection.ReadAsync(token);
			if (fragment == null)
				throw new IOException("A conexão terminou antes do marcador.");
			output.Append(fragment);
		}
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
