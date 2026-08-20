using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class DockerManager
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

	private readonly MissionInfrastructureCatalog _catalog;

	public DockerManager(MissionInfrastructureCatalog catalog)
	{
		_catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
	}

	public async Task<DockerContainerState> InspectContainerAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		DockerCommandResult result = await RunDockerAsync(
			"inspecionar container",
			new[]
			{
				"container",
				"inspect",
				"--format",
				"{{.State.Running}}",
				definition.ContainerName
			},
			allowFailure: true,
			cancellationToken: cancellationToken
		);

		if (result.ExitCode != 0)
		{
			if (IsMissingResource(result.StandardError))
				return DockerContainerState.Missing;

			throw CreateFailure("inspecionar container", result.StandardError);
		}

		bool running = string.Equals(
			result.StandardOutput.Trim(),
			"true",
			StringComparison.OrdinalIgnoreCase
		);

		return new DockerContainerState(true, running);
	}

	public async Task EnsureContainerCreatedAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		DockerContainerState state = await InspectContainerAsync(
			infrastructureId,
			cancellationToken
		);

		if (state.Exists)
			return;

		var arguments = new List<string>
		{
			"container",
			"create",
			"--name",
			definition.ContainerName
		};

		foreach (MissionHostBinding binding in definition.HostBindings)
		{
			arguments.Add("--publish");
			arguments.Add(
				$"{binding.HostIp}:{binding.HostPort}:{binding.ContainerPort}"
			);
		}

		arguments.Add(definition.Image);

		await RunDockerAsync(
			"criar container",
			arguments,
			cancellationToken: cancellationToken
		);
	}

	public async Task StartContainerAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		await EnsureContainerCreatedAsync(infrastructureId, cancellationToken);

		DockerContainerState state = await InspectContainerAsync(
			infrastructureId,
			cancellationToken
		);
		if (state.IsRunning)
			return;

		await RunDockerAsync(
			"iniciar container",
			new[] { "container", "start", definition.ContainerName },
			cancellationToken: cancellationToken
		);
	}

	public async Task StopContainerAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		DockerContainerState state = await InspectContainerAsync(
			infrastructureId,
			cancellationToken
		);

		if (!state.Exists || !state.IsRunning)
			return;

		await RunDockerAsync(
			"parar container",
			new[] { "container", "stop", definition.ContainerName },
			timeout: StopTimeout,
			cancellationToken: cancellationToken
		);
	}

	public async Task RemoveContainerAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		DockerContainerState state = await InspectContainerAsync(
			infrastructureId,
			cancellationToken
		);

		if (!state.Exists)
			return;

		await RunDockerAsync(
			"remover container",
			new[] { "container", "remove", "--force", definition.ContainerName },
			timeout: StopTimeout,
			cancellationToken: cancellationToken
		);
	}

	public async Task EnsureNetworkCreatedAsync(
		string networkName,
		CancellationToken cancellationToken = default
	)
	{
		networkName = _catalog.RequireOwnedNetwork(networkName);
		DockerCommandResult inspectResult = await RunDockerAsync(
			"inspecionar rede",
			new[] { "network", "inspect", networkName },
			allowFailure: true,
			cancellationToken: cancellationToken
		);

		if (inspectResult.ExitCode == 0)
			return;

		if (!IsMissingResource(inspectResult.StandardError))
			throw CreateFailure("inspecionar rede", inspectResult.StandardError);

		await RunDockerAsync(
			"criar rede",
			new[] { "network", "create", networkName },
			cancellationToken: cancellationToken
		);
	}

	public async Task RemoveNetworkAsync(
		string networkName,
		CancellationToken cancellationToken = default
	)
	{
		networkName = _catalog.RequireOwnedNetwork(networkName);
		DockerCommandResult inspectResult = await RunDockerAsync(
			"inspecionar rede",
			new[] { "network", "inspect", networkName },
			allowFailure: true,
			cancellationToken: cancellationToken
		);

		if (inspectResult.ExitCode != 0)
		{
			if (IsMissingResource(inspectResult.StandardError))
				return;

			throw CreateFailure("inspecionar rede", inspectResult.StandardError);
		}

		await RunDockerAsync(
			"remover rede",
			new[] { "network", "remove", networkName },
			cancellationToken: cancellationToken
		);
	}

	public async Task ConnectNetworkAsync(
		string infrastructureId,
		string networkName,
		string networkAlias,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		networkName = _catalog.RequireOwnedNetwork(networkName);
		RequireDeclaredAlias(definition, networkName, networkAlias);

		IReadOnlyDictionary<string, string> addresses =
			await GetContainerNetworkAddressesAsync(
				infrastructureId,
				cancellationToken
			);
		if (addresses.ContainsKey(networkName))
			return;

		await RunDockerAsync(
			"conectar container à rede",
			new[]
			{
				"network",
				"connect",
				"--alias",
				networkAlias,
				networkName,
				definition.ContainerName
			},
			cancellationToken: cancellationToken
		);
	}

	public async Task<string> ResolveContainerIpAsync(
		string infrastructureId,
		string networkName,
		CancellationToken cancellationToken = default
	)
	{
		_catalog.GetDefinition(infrastructureId);
		networkName = _catalog.RequireOwnedNetwork(networkName);

		IReadOnlyDictionary<string, string> addresses =
			await GetContainerNetworkAddressesAsync(
				infrastructureId,
				cancellationToken
			);

		if (
			!addresses.TryGetValue(networkName, out string address) ||
			!IPAddress.TryParse(address, out _)
		)
		{
			throw new DockerOperationException(
				"O container ainda não possui um IP válido na rede da missão."
			);
		}

		return address;
	}

	public async Task<bool> ProbePlayerReadinessAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		if (definition.Kind != MissionInfrastructureKind.Player)
			throw new ArgumentException("A infraestrutura não é do tipo player.");

		MissionHostBinding binding = definition.HostBindings.FirstOrDefault(
			candidate => candidate.ContainerPort == definition.Readiness.Port
		);
		if (binding == null)
		{
			throw new DockerOperationException(
				"A porta de readiness do player não possui vínculo local."
			);
		}

		using var client = new TcpClient();
		using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken
		);
		probeCancellation.CancelAfter(ProbeTimeout);

		try
		{
			await client.ConnectAsync(
				binding.HostIp,
				binding.HostPort,
				probeCancellation.Token
			);
			return client.Connected;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		catch (SocketException)
		{
			return false;
		}
	}

	public async Task<bool> ProbeMissionReadinessAsync(
		string infrastructureId,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition mission =
			_catalog.GetDefinition(infrastructureId);
		if (
			mission.Kind != MissionInfrastructureKind.Mission ||
			mission.Network == null
		)
		{
			throw new ArgumentException("A infraestrutura não é do tipo missão.");
		}

		MissionInfrastructureDefinition source =
			_catalog.GetByContainerName(mission.Readiness.FromContainer);
		DockerCommandResult result = await RunDockerAsync(
			"verificar readiness da missão",
			new[]
			{
				"container",
				"exec",
				source.ContainerName,
				"nc",
				"-z",
				"-w",
				"2",
				mission.Network.Alias,
				mission.Readiness.Port.ToString()
			},
			allowFailure: true,
			timeout: ProbeTimeout,
			cancellationToken: cancellationToken
		);

		return result.ExitCode == 0;
	}

	public async Task<DockerExecResult> ExecuteAsync(
		string infrastructureId,
		IReadOnlyList<string> command,
		string standardInput = null,
		CancellationToken cancellationToken = default
	)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		if (command == null || command.Count == 0)
			throw new ArgumentException("O comando do container está vazio.");

		if (command.Any(argument => argument == null || argument.Contains('\0')))
			throw new ArgumentException("O comando do container contém argumento inválido.");

		var arguments = new List<string> { "container", "exec" };
		if (standardInput != null)
			arguments.Add("--interactive");

		arguments.Add(definition.ContainerName);
		arguments.AddRange(command);

		DockerCommandResult result = await RunDockerAsync(
			"executar comando no container",
			arguments,
			standardInput,
			cancellationToken: cancellationToken
		);

		return new DockerExecResult(result.StandardOutput, result.ExitCode);
	}

	private async Task<IReadOnlyDictionary<string, string>>
		GetContainerNetworkAddressesAsync(
			string infrastructureId,
			CancellationToken cancellationToken
		)
	{
		MissionInfrastructureDefinition definition =
			_catalog.GetDefinition(infrastructureId);
		DockerCommandResult result = await RunDockerAsync(
			"inspecionar redes do container",
			new[] { "container", "inspect", definition.ContainerName },
			cancellationToken: cancellationToken
		);

		try
		{
			using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
			JsonElement networks = document.RootElement[0]
				.GetProperty("NetworkSettings")
				.GetProperty("Networks");
			var addresses = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (JsonProperty network in networks.EnumerateObject())
			{
				string address = network.Value
					.GetProperty("IPAddress")
					.GetString() ?? "";
				addresses[network.Name] = address;
			}

			return addresses;
		}
		catch (Exception exception) when (
			exception is JsonException ||
			exception is InvalidOperationException ||
			exception is KeyNotFoundException ||
			exception is IndexOutOfRangeException
		)
		{
			throw new DockerOperationException(
				"O Docker retornou dados de rede inválidos.",
				exception
			);
		}
	}

	private void RequireDeclaredAlias(
		MissionInfrastructureDefinition definition,
		string networkName,
		string networkAlias
	)
	{
		bool isDeclaredMissionAlias =
			definition.Network?.Name == networkName &&
			definition.Network.Alias == networkAlias;
		bool isDeclaredPlayerAlias =
			definition.Kind == MissionInfrastructureKind.Player &&
			definition.NetworkAlias == networkAlias;

		if (!isDeclaredMissionAlias && !isDeclaredPlayerAlias)
		{
			throw new ArgumentException(
				"O alias solicitado não pertence à infraestrutura declarada."
			);
		}
	}

	private async Task<DockerCommandResult> RunDockerAsync(
		string operation,
		IReadOnlyList<string> arguments,
		string standardInput = null,
		bool allowFailure = false,
		TimeSpan? timeout = null,
		CancellationToken cancellationToken = default
	)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "docker",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			RedirectStandardInput = standardInput != null,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		foreach (string argument in arguments)
			startInfo.ArgumentList.Add(argument);

		using var process = new Process { StartInfo = startInfo };
		using var timeoutCancellation =
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCancellation.CancelAfter(timeout ?? DefaultTimeout);

		try
		{
			if (!process.Start())
				throw new DockerOperationException($"Não foi possível {operation}.");

			Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
			Task<string> stderrTask = process.StandardError.ReadToEndAsync();

			if (standardInput != null)
			{
				await process.StandardInput.WriteAsync(
					standardInput.AsMemory(),
					timeoutCancellation.Token
				);
				process.StandardInput.Close();
			}

			await process.WaitForExitAsync(timeoutCancellation.Token);
			string standardOutput = await stdoutTask;
			string standardError = await stderrTask;
			var result = new DockerCommandResult(
				process.ExitCode,
				standardOutput,
				standardError
			);

			if (!allowFailure && result.ExitCode != 0)
				throw CreateFailure(operation, result.StandardError);

			return result;
		}
		catch (OperationCanceledException)
		{
			TryKill(process);

			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException(cancellationToken);

			throw new DockerOperationException(
				$"A operação para {operation} excedeu o tempo limite."
			);
		}
		catch (DockerOperationException)
		{
			throw;
		}
		catch (Exception exception)
		{
			TryKill(process);
			throw new DockerOperationException(
				$"Não foi possível {operation}. Verifique se o Docker está disponível.",
				exception
			);
		}
	}

	private static DockerOperationException CreateFailure(
		string operation,
		string standardError
	)
	{
		string detail = SanitizeError(standardError);
		string message = string.IsNullOrWhiteSpace(detail)
			? $"Não foi possível {operation}."
			: $"Não foi possível {operation}: {detail}";
		return new DockerOperationException(message);
	}

	private static string SanitizeError(string error)
	{
		if (string.IsNullOrWhiteSpace(error))
			return "";

		var builder = new StringBuilder();
		foreach (char character in error.Trim())
		{
			if (builder.Length >= 300)
				break;

			if (!char.IsControl(character))
				builder.Append(character);
			else if (character == '\r' || character == '\n' || character == '\t')
				builder.Append(' ');
		}

		return builder.ToString();
	}

	private static bool IsMissingResource(string error)
	{
		return
			(error ?? "").Contains("No such", StringComparison.OrdinalIgnoreCase) ||
			(error ?? "").Contains("not found", StringComparison.OrdinalIgnoreCase);
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (process != null && !process.HasExited)
				process.Kill(entireProcessTree: true);
		}
		catch
		{
			// Best effort after cancellation or process-start failure.
		}
	}

	private sealed class DockerCommandResult
	{
		public int ExitCode { get; }
		public string StandardOutput { get; }
		public string StandardError { get; }

		public DockerCommandResult(
			int exitCode,
			string standardOutput,
			string standardError
		)
		{
			ExitCode = exitCode;
			StandardOutput = standardOutput ?? "";
			StandardError = standardError ?? "";
		}
	}
}

public sealed class DockerContainerState
{
	public static DockerContainerState Missing { get; } = new(false, false);

	public bool Exists { get; }
	public bool IsRunning { get; }

	public DockerContainerState(bool exists, bool isRunning)
	{
		Exists = exists;
		IsRunning = isRunning;
	}
}

public sealed class DockerExecResult
{
	public string StandardOutput { get; }
	public int ExitCode { get; }

	public DockerExecResult(string standardOutput, int exitCode)
	{
		StandardOutput = standardOutput ?? "";
		ExitCode = exitCode;
	}
}

public sealed class DockerOperationException : Exception
{
	public DockerOperationException(string message) : base(message)
	{
	}

	public DockerOperationException(string message, Exception innerException) :
		base(message, innerException)
	{
	}
}
