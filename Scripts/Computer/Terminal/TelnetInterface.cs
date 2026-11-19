using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TelnetInterface
{
	enum Verbs
	{
		SE = 240,
		SB = 250,
		WILL = 251,
		WONT = 252,
		DO = 253,
		DONT = 254,
		IAC = 255
	}

	enum Options
	{
		BINARY = 0,
		ECHO = 1,
		SGA = 3,
		TERMINAL_TYPE = 24,
		NAWS = 31
	}

	enum TelnetParseState
	{
		Data,
		Iac,
		NegotiationOption,
		Subnegotiation,
		SubnegotiationIac
	}

	public sealed class TelnetConnection : IDisposable
	{
		private const int ReadBufferSize = 4096;
		private static readonly Regex _loginControlSequences = new(
			@"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\))",
			RegexOptions.CultureInvariant
		);

		private readonly TcpClient _tcpSocket;
		private readonly NetworkStream _networkStream;
		private readonly Decoder _utf8Decoder = Encoding.UTF8.GetDecoder();
		private readonly SemaphoreSlim _writeLock = new(1, 1);
		private readonly byte[] _readBuffer = new byte[ReadBufferSize];
		private readonly HashSet<byte> _localOptions = new();
		private readonly HashSet<byte> _remoteOptions = new();
		private readonly List<byte> _subnegotiation = new();
		private int _columns = 80;
		private int _rows = 24;

		private TelnetParseState _parseState = TelnetParseState.Data;
		private Verbs _negotiationVerb;
		private bool _remoteClosed;
		private int _closed;
		private int _disposed;

		private TelnetConnection(TcpClient tcpSocket)
		{
			_tcpSocket = tcpSocket;
			_networkStream = tcpSocket.GetStream();
		}

		public static async Task<TelnetConnection> ConnectAsync(
			string hostname,
			int port,
			CancellationToken cancellationToken
		)
		{
			var tcpSocket = new TcpClient();

			try
			{
				await tcpSocket.ConnectAsync(
					hostname,
					port,
					cancellationToken
				);

				return new TelnetConnection(tcpSocket);
			}
			catch
			{
				tcpSocket.Dispose();
				throw;
			}
		}

		public async Task<string> LoginAsync(
			string username,
			string password,
			int loginTimeoutMs,
			CancellationToken cancellationToken
		)
		{
			using var timeoutCts =
				CancellationTokenSource.CreateLinkedTokenSource(
					cancellationToken
				);

			timeoutCts.CancelAfter(Math.Max(1, loginTimeoutMs));
			CancellationToken token = timeoutCts.Token;
			var output = new StringBuilder();

			try
			{
				await ReadUntilAsync(
					output,
					value => EndsWithPrompt(value, "login:"),
					token
				);

				await WriteLineAsync(username, token);

				await ReadUntilAsync(
					output,
					value => EndsWithPrompt(value, "password:"),
					token
				);

				await WriteLineAsync(password, token);

				await ReadUntilAsync(
					output,
					EndsWithShellPrompt,
					token
				);

				return output.ToString();
			}
			catch (OperationCanceledException)
				when (
					!cancellationToken.IsCancellationRequested &&
					timeoutCts.IsCancellationRequested
				)
			{
				throw new TimeoutException(
					"A conexão não apresentou os prompts de login no tempo esperado."
				);
			}
		}

		public async Task WriteLineAsync(
			string command,
			CancellationToken cancellationToken
		)
		{
			await WriteAsync(command + "\n", cancellationToken);
		}

		public async Task WriteAsync(
			string value,
			CancellationToken cancellationToken
		)
		{
			ArgumentNullException.ThrowIfNull(value);
			await WriteAsync(Encoding.UTF8.GetBytes(value), cancellationToken);
		}

		public async Task WriteAsync(
			byte[] bytes,
			CancellationToken cancellationToken
		)
		{
			ArgumentNullException.ThrowIfNull(bytes);
			ThrowIfClosed();

			await WriteRawAsync(EscapeTelnetData(bytes), cancellationToken);
		}

		public async Task SetWindowSizeAsync(
			int columns,
			int rows,
			CancellationToken cancellationToken
		)
		{
			await _writeLock.WaitAsync(cancellationToken);

			try
			{
				ThrowIfClosed();
				columns = Math.Clamp(columns, 1, ushort.MaxValue);
				rows = Math.Clamp(rows, 1, ushort.MaxValue);

				if (_columns == columns && _rows == rows)
					return;

				_columns = columns;
				_rows = rows;

				if (_localOptions.Contains((byte)Options.NAWS))
				{
					var response = new List<byte>();
					AppendWindowSize(response);
					await _networkStream.WriteAsync(response.ToArray(), cancellationToken);
				}
			}
			finally
			{
				_writeLock.Release();
			}
		}

		public async Task<string> ReadAsync(
			CancellationToken cancellationToken
		)
		{
			byte[] data = await ReadDataAsync(cancellationToken);

			if (data != null)
				return DecodeUtf8(data, false);

			string remainingText = DecodeUtf8(ReadOnlySpan<byte>.Empty, true);
			return remainingText.Length == 0 ? null : remainingText;
		}

		public async Task<byte[]> ReadDataAsync(
			CancellationToken cancellationToken
		)
		{
			ThrowIfClosed();

			if (_remoteClosed)
				return null;

			int bytesRead = await _networkStream.ReadAsync(
				_readBuffer.AsMemory(0, _readBuffer.Length),
				cancellationToken
			);

			if (bytesRead == 0)
			{
				_remoteClosed = true;
				return null;
			}

			byte[] applicationData = new byte[bytesRead];
			int applicationDataLength = 0;
			var negotiationResponse = new List<byte>();

			await _writeLock.WaitAsync(cancellationToken);
			try
			{
				for (int index = 0; index < bytesRead; index++)
				{
					ParseTelnetByte(
						_readBuffer[index],
						applicationData,
						ref applicationDataLength,
						negotiationResponse
					);
				}

				if (negotiationResponse.Count > 0)
				{
					await _networkStream.WriteAsync(
						negotiationResponse.ToArray(),
						cancellationToken
					);
				}
			}
			finally
			{
				_writeLock.Release();
			}

			Array.Resize(ref applicationData, applicationDataLength);
			return applicationData;
		}

		public bool IsConnected
		{
			get
			{
				return Volatile.Read(ref _closed) == 0 && !_remoteClosed;
			}
		}

		public void Close()
		{
			if (Interlocked.Exchange(ref _closed, 1) != 0)
				return;

			try
			{
				_networkStream.Dispose();
			}
			finally
			{
				_tcpSocket.Dispose();
			}
		}

		public void Dispose()
		{
			Close();

			if (Interlocked.Exchange(ref _disposed, 1) == 0)
				_writeLock.Dispose();
		}

		private async Task ReadUntilAsync(
			StringBuilder output,
			Func<StringBuilder, bool> isComplete,
			CancellationToken cancellationToken
		)
		{
			// Inspect the prompt without changing the bytes displayed by the emulator.
			while (!isComplete(new StringBuilder(
				_loginControlSequences.Replace(output.ToString(), string.Empty)
			)))
			{
				string fragment = await ReadAsync(cancellationToken);

				if (fragment == null)
				{
					throw new IOException(
						"A conexão Telnet foi encerrada durante o login."
					);
				}

				output.Append(fragment);
			}
		}

		private async Task WriteRawAsync(
			ReadOnlyMemory<byte> bytes,
			CancellationToken cancellationToken
		)
		{
			await _writeLock.WaitAsync(cancellationToken);

			try
			{
				ThrowIfClosed();
				await _networkStream.WriteAsync(bytes, cancellationToken);
			}
			finally
			{
				_writeLock.Release();
			}
		}

		private void ParseTelnetByte(
			byte input,
			byte[] applicationData,
			ref int applicationDataLength,
			List<byte> negotiationResponse
		)
		{
			switch (_parseState)
			{
				case TelnetParseState.Data:
					if (input == (byte)Verbs.IAC)
						_parseState = TelnetParseState.Iac;
					else
						applicationData[applicationDataLength++] = input;
					break;

				case TelnetParseState.Iac:
					ParseIacByte(
						input,
						applicationData,
						ref applicationDataLength
					);
					break;

				case TelnetParseState.NegotiationOption:
					AppendNegotiationResponse(
						_negotiationVerb,
						input,
						negotiationResponse
					);
					_parseState = TelnetParseState.Data;
					break;

				case TelnetParseState.Subnegotiation:
					if (input == (byte)Verbs.IAC)
					{
						_parseState = TelnetParseState.SubnegotiationIac;
					}
					else if (_subnegotiation.Count < 128)
					{
						_subnegotiation.Add(input);
					}
					break;

				case TelnetParseState.SubnegotiationIac:
					if (input == (byte)Verbs.SE)
						AppendSubnegotiationResponse(negotiationResponse);
					else if (input == (byte)Verbs.IAC && _subnegotiation.Count < 128)
						_subnegotiation.Add(input);

					_parseState = input == (byte)Verbs.SE
						? TelnetParseState.Data
						: TelnetParseState.Subnegotiation;
					break;
			}
		}

		private void ParseIacByte(
			byte input,
			byte[] applicationData,
			ref int applicationDataLength
		)
		{
			if (input == (byte)Verbs.IAC)
			{
				applicationData[applicationDataLength++] = input;
				_parseState = TelnetParseState.Data;
				return;
			}

			if (input == (byte)Verbs.SB)
			{
				_subnegotiation.Clear();
				_parseState = TelnetParseState.Subnegotiation;
				return;
			}

			if (
				input == (byte)Verbs.DO ||
				input == (byte)Verbs.DONT ||
				input == (byte)Verbs.WILL ||
				input == (byte)Verbs.WONT
			)
			{
				_negotiationVerb = (Verbs)input;
				_parseState = TelnetParseState.NegotiationOption;
				return;
			}

			_parseState = TelnetParseState.Data;
		}

		private void AppendNegotiationResponse(
			Verbs verb,
			byte option,
			List<byte> response
		)
		{
			switch (verb)
			{
				case Verbs.DO:
					if (!IsAcceptedClientOption(option))
					{
						AppendOption(response, Verbs.WONT, option);
					}
					else if (_localOptions.Add(option))
					{
						AppendOption(response, Verbs.WILL, option);

						if (option == (byte)Options.NAWS)
							AppendWindowSize(response);
					}
					break;

				case Verbs.WILL:
					if (!IsAcceptedServerOption(option))
						AppendOption(response, Verbs.DONT, option);
					else if (_remoteOptions.Add(option))
						AppendOption(response, Verbs.DO, option);
					break;

				case Verbs.DONT:
					if (_localOptions.Remove(option))
						AppendOption(response, Verbs.WONT, option);
					break;

				case Verbs.WONT:
					if (_remoteOptions.Remove(option))
						AppendOption(response, Verbs.DONT, option);
					break;
			}
		}

		private static void AppendOption(List<byte> response, Verbs verb, byte option)
		{
			response.Add((byte)Verbs.IAC);
			response.Add((byte)verb);
			response.Add(option);
		}

		private void AppendSubnegotiationResponse(List<byte> response)
		{
			if (
				_subnegotiation.Count == 2 &&
				_subnegotiation[0] == (byte)Options.TERMINAL_TYPE &&
				_subnegotiation[1] == 1 &&
				_localOptions.Contains((byte)Options.TERMINAL_TYPE)
			)
			{
				AppendSubnegotiation(
					response,
					Options.TERMINAL_TYPE,
					Encoding.ASCII.GetBytes("\0xterm-256color")
				);
			}
		}

		private void AppendWindowSize(List<byte> response)
		{
			AppendSubnegotiation(
				response,
				Options.NAWS,
				new byte[]
				{
					(byte)(_columns >> 8), (byte)_columns,
					(byte)(_rows >> 8), (byte)_rows
				}
			);
		}

		private static void AppendSubnegotiation(
			List<byte> response,
			Options option,
			byte[] payload
		)
		{
			AppendOption(response, Verbs.SB, (byte)option);
			response.AddRange(EscapeTelnetData(payload));
			response.Add((byte)Verbs.IAC);
			response.Add((byte)Verbs.SE);
		}

		private static bool IsAcceptedClientOption(byte option)
		{
			return option is
				(byte)Options.BINARY or
				(byte)Options.SGA or
				(byte)Options.TERMINAL_TYPE or
				(byte)Options.NAWS;
		}

		private static bool IsAcceptedServerOption(byte option)
		{
			return
				option == (byte)Options.BINARY ||
				option == (byte)Options.ECHO ||
				option == (byte)Options.SGA;
		}

		private string DecodeUtf8(
			ReadOnlySpan<byte> bytes,
			bool flush
		)
		{
			char[] characters = new char[
				Encoding.UTF8.GetMaxCharCount(bytes.Length)
			];

			_utf8Decoder.Convert(
				bytes,
				characters.AsSpan(),
				flush,
				out _,
				out int charactersUsed,
				out _
			);

			return new string(characters, 0, charactersUsed);
		}

		private static byte[] EscapeTelnetData(byte[] bytes)
		{
			int iacCount = 0;

			foreach (byte value in bytes)
			{
				if (value == (byte)Verbs.IAC)
					iacCount++;
			}

			if (iacCount == 0)
				return bytes;

			byte[] escaped = new byte[bytes.Length + iacCount];
			int outputIndex = 0;

			foreach (byte value in bytes)
			{
				escaped[outputIndex++] = value;

				if (value == (byte)Verbs.IAC)
					escaped[outputIndex++] = value;
			}

			return escaped;
		}

		private static bool EndsWithPrompt(
			StringBuilder output,
			string prompt
		)
		{
			int outputIndex = LastNonWhitespaceIndex(output);

			for (int promptIndex = prompt.Length - 1; promptIndex >= 0; promptIndex--)
			{
				if (outputIndex < 0)
					return false;

				char actual = char.ToUpperInvariant(output[outputIndex--]);
				char expected = char.ToUpperInvariant(prompt[promptIndex]);

				if (actual != expected)
					return false;
			}

			return true;
		}

		private static bool EndsWithShellPrompt(StringBuilder output)
		{
			int promptIndex = LastNonWhitespaceIndex(output);

			if (promptIndex < 0)
				return false;

			return output[promptIndex] is '$' or '#';
		}

		private static int LastNonWhitespaceIndex(StringBuilder output)
		{
			int index = output.Length - 1;

			while (
				index >= 0 &&
				(
					char.IsWhiteSpace(output[index]) ||
					output[index] == '\0'
				)
			)
			{
				index--;
			}

			return index;
		}

		private void ThrowIfClosed()
		{
			ObjectDisposedException.ThrowIf(
				Volatile.Read(ref _closed) != 0,
				this
			);
		}
	}
}
