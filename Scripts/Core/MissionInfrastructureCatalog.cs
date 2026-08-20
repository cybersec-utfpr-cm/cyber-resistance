using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public sealed class MissionInfrastructureCatalog
{
	private static readonly Regex DockerNamePattern = new(
		@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$",
		RegexOptions.Compiled
	);

	private static readonly Regex ImageNamePattern = new(
		@"^[A-Za-z0-9][A-Za-z0-9_./-]*(?::[A-Za-z0-9_.-]+)?$",
		RegexOptions.Compiled
	);

	private static readonly Regex UnixNamePattern = new(
		@"^[A-Za-z_][A-Za-z0-9_-]{0,31}$",
		RegexOptions.Compiled
	);

	private static readonly Regex AbsolutePathPattern = new(
		@"^/[A-Za-z0-9_./-]+$",
		RegexOptions.Compiled
	);

	private static readonly Regex FileModePattern = new(
		@"^0[0-7]{3}$",
		RegexOptions.Compiled
	);

	private readonly Dictionary<string, MissionInfrastructureDefinition>
		_definitions;
	private readonly Dictionary<string, MissionInfrastructureDefinition>
		_definitionsByContainer;
	private readonly HashSet<string> _ownedNetworks;

	private MissionInfrastructureCatalog(
		Dictionary<string, MissionInfrastructureDefinition> definitions,
		Dictionary<string, MissionInfrastructureDefinition> definitionsByContainer,
		HashSet<string> ownedNetworks
	)
	{
		_definitions = definitions;
		_definitionsByContainer = definitionsByContainer;
		_ownedNetworks = ownedNetworks;
	}

	public IReadOnlyCollection<MissionInfrastructureDefinition> Definitions =>
		_definitions.Values;

	public IReadOnlyCollection<string> OwnedNetworks => _ownedNetworks;

	public static MissionInfrastructureCatalog Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			throw new InvalidDataException("O catálogo de infraestrutura está vazio.");

		MissionInfrastructureCatalogDocument document;

		try
		{
			document = JsonSerializer.Deserialize<MissionInfrastructureCatalogDocument>(
				json
			);
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException(
				"O catálogo de infraestrutura contém JSON inválido.",
				exception
			);
		}

		if (document?.Infrastructures == null || document.Infrastructures.Count == 0)
		{
			throw new InvalidDataException(
				"O catálogo deve declarar pelo menos uma infraestrutura."
			);
		}

		var definitions = new Dictionary<
			string,
			MissionInfrastructureDefinition
		>(StringComparer.Ordinal);
		var definitionsByContainer = new Dictionary<
			string,
			MissionInfrastructureDefinition
		>(StringComparer.Ordinal);
		var ownedNetworks = new HashSet<string>(StringComparer.Ordinal);
		var hostBindings = new HashSet<string>(StringComparer.Ordinal);

		foreach (MissionInfrastructureDefinition definition in document.Infrastructures)
		{
			ValidateDefinition(definition);

			if (!definitions.TryAdd(definition.Id, definition))
			{
				throw new InvalidDataException(
					$"A infraestrutura '{definition.Id}' está duplicada."
				);
			}

			if (!definitionsByContainer.TryAdd(definition.ContainerName, definition))
			{
				throw new InvalidDataException(
					$"O container '{definition.ContainerName}' está duplicado."
				);
			}

			foreach (MissionHostBinding binding in definition.HostBindings)
			{
				string bindingKey = $"{binding.HostIp}:{binding.HostPort}";
				if (!hostBindings.Add(bindingKey))
				{
					throw new InvalidDataException(
						$"O vínculo local '{bindingKey}' está duplicado."
					);
				}
			}

			if (definition.Network != null)
				ownedNetworks.Add(definition.Network.Name);
		}

		if (
			definitions.Values.Count(
				definition => definition.Kind == MissionInfrastructureKind.Player
			) != 1
		)
		{
			throw new InvalidDataException(
				"O catálogo deve declarar exatamente uma infraestrutura player."
			);
		}

		ValidateReferences(definitions, definitionsByContainer);

		return new MissionInfrastructureCatalog(
			definitions,
			definitionsByContainer,
			ownedNetworks
		);
	}

	public MissionInfrastructureDefinition GetDefinition(string infrastructureId)
	{
		if (
			string.IsNullOrWhiteSpace(infrastructureId) ||
			!_definitions.TryGetValue(infrastructureId, out var definition)
		)
		{
			throw new ArgumentException(
				$"Infraestrutura não declarada: '{infrastructureId}'.",
				nameof(infrastructureId)
			);
		}

		return definition;
	}

	public MissionInfrastructureDefinition GetByContainerName(string containerName)
	{
		if (
			string.IsNullOrWhiteSpace(containerName) ||
			!_definitionsByContainer.TryGetValue(containerName, out var definition)
		)
		{
			throw new ArgumentException(
				$"Container não declarado: '{containerName}'.",
				nameof(containerName)
			);
		}

		return definition;
	}

	public string RequireOwnedNetwork(string networkName)
	{
		if (
			string.IsNullOrWhiteSpace(networkName) ||
			!_ownedNetworks.Contains(networkName)
		)
		{
			throw new ArgumentException(
				$"Rede não declarada: '{networkName}'.",
				nameof(networkName)
			);
		}

		return networkName;
	}

	private static void ValidateDefinition(
		MissionInfrastructureDefinition definition
	)
	{
		if (definition == null)
			throw new InvalidDataException("A infraestrutura não pode ser nula.");

		RequireDockerName(definition.Id, "id da infraestrutura");

		if (
			definition.Kind != MissionInfrastructureKind.Player &&
			definition.Kind != MissionInfrastructureKind.Mission
		)
		{
			throw new InvalidDataException(
				$"A infraestrutura '{definition.Id}' possui kind inválido."
			);
		}

		RequireDockerName(definition.ContainerName, "nome do container");
		if (!ImageNamePattern.IsMatch(definition.Image ?? ""))
		{
			throw new InvalidDataException(
				$"A infraestrutura '{definition.Id}' possui imagem inválida."
			);
		}

		definition.HostBindings ??= new List<MissionHostBinding>();
		foreach (MissionHostBinding binding in definition.HostBindings)
			ValidateHostBinding(definition.Id, binding);

		if (definition.Readiness == null)
		{
			throw new InvalidDataException(
				$"A infraestrutura '{definition.Id}' não declara readiness."
			);
		}

		if (definition.Readiness.Type != "tcp")
		{
			throw new InvalidDataException(
				$"A infraestrutura '{definition.Id}' possui readiness inválido."
			);
		}

		RequirePort(definition.Readiness.Port, "porta de readiness");

		if (definition.Kind == MissionInfrastructureKind.Player)
		{
			RequireDockerName(definition.NetworkAlias, "alias do player");
			if (
				!definition.HostBindings.Any(
					binding =>
						binding.ContainerPort == definition.Readiness.Port
				)
			)
			{
				throw new InvalidDataException(
					$"A infraestrutura player '{definition.Id}' não vincula sua porta de readiness."
				);
			}

			if (definition.Network != null)
			{
				throw new InvalidDataException(
					$"A infraestrutura player '{definition.Id}' não deve possuir rede própria."
				);
			}
		}
		else
		{
			RequireDockerName(
				definition.PlayerInfrastructureId,
				"id da infraestrutura player"
			);
			ValidateNetwork(definition.Id, definition.Network);
			RequireDockerName(
				definition.Readiness.FromContainer,
				"origem do readiness"
			);
		}

		if (definition.Credentials != null)
		{
			if (
				string.IsNullOrWhiteSpace(definition.Credentials.Username) ||
				string.IsNullOrWhiteSpace(definition.Credentials.Password)
			)
			{
				throw new InvalidDataException(
					$"A infraestrutura '{definition.Id}' possui credenciais incompletas."
				);
			}
		}

		if (definition.FlagTarget != null)
			ValidateFlagTarget(definition.Id, definition.FlagTarget);
	}

	private static void ValidateReferences(
		IReadOnlyDictionary<string, MissionInfrastructureDefinition> definitions,
		IReadOnlyDictionary<string, MissionInfrastructureDefinition> byContainer
	)
	{
		foreach (
			MissionInfrastructureDefinition definition in
			definitions.Values.Where(
				candidate => candidate.Kind == MissionInfrastructureKind.Mission
			)
		)
		{
			if (
				!definitions.TryGetValue(
					definition.PlayerInfrastructureId,
					out var player
				) ||
				player.Kind != MissionInfrastructureKind.Player
			)
			{
				throw new InvalidDataException(
					$"A infraestrutura '{definition.Id}' referencia um player inválido."
				);
			}

			if (
				!byContainer.TryGetValue(
					definition.Readiness.FromContainer,
					out var readinessSource
				) ||
				readinessSource.Id != player.Id
			)
			{
				throw new InvalidDataException(
					$"A infraestrutura '{definition.Id}' possui origem de readiness inválida."
				);
			}
		}
	}

	private static void ValidateHostBinding(
		string infrastructureId,
		MissionHostBinding binding
	)
	{
		if (binding == null || binding.HostIp != "127.0.0.1")
		{
			throw new InvalidDataException(
				$"A infraestrutura '{infrastructureId}' deve vincular portas somente em 127.0.0.1."
			);
		}

		RequirePort(binding.HostPort, "porta local");
		RequirePort(binding.ContainerPort, "porta do container");
	}

	private static void ValidateNetwork(
		string infrastructureId,
		MissionNetworkDefinition network
	)
	{
		if (network == null)
		{
			throw new InvalidDataException(
				$"A infraestrutura de missão '{infrastructureId}' não declara rede."
			);
		}

		RequireDockerName(network.Name, "nome da rede");
		RequireDockerName(network.Alias, "alias da missão");
	}

	private static void ValidateFlagTarget(
		string infrastructureId,
		MissionFlagTarget flagTarget
	)
	{
		if (!AbsolutePathPattern.IsMatch(flagTarget.Path ?? ""))
		{
			throw new InvalidDataException(
				$"A infraestrutura '{infrastructureId}' possui caminho de flag inválido."
			);
		}

		if (
			!UnixNamePattern.IsMatch(flagTarget.Owner ?? "") ||
			!UnixNamePattern.IsMatch(flagTarget.Group ?? "") ||
			!FileModePattern.IsMatch(flagTarget.Mode ?? "")
		)
		{
			throw new InvalidDataException(
				$"A infraestrutura '{infrastructureId}' possui metadados de flag inválidos."
			);
		}
	}

	private static void RequireDockerName(string value, string fieldName)
	{
		if (!DockerNamePattern.IsMatch(value ?? ""))
			throw new InvalidDataException($"O {fieldName} é inválido.");
	}

	private static void RequirePort(int port, string fieldName)
	{
		if (port < 1 || port > 65535)
			throw new InvalidDataException($"A {fieldName} é inválida.");
	}
}

public static class MissionInfrastructureKind
{
	public const string Player = "player";
	public const string Mission = "mission";
}

public sealed class MissionInfrastructureDefinition
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("kind")]
	public string Kind { get; set; } = "";

	[JsonPropertyName("player_infrastructure_id")]
	public string PlayerInfrastructureId { get; set; } = "";

	[JsonPropertyName("container_name")]
	public string ContainerName { get; set; } = "";

	[JsonPropertyName("image")]
	public string Image { get; set; } = "";

	[JsonPropertyName("network_alias")]
	public string NetworkAlias { get; set; } = "";

	[JsonPropertyName("network")]
	public MissionNetworkDefinition Network { get; set; }

	[JsonPropertyName("host_bindings")]
	public List<MissionHostBinding> HostBindings { get; set; } = new();

	[JsonPropertyName("readiness")]
	public MissionReadinessDefinition Readiness { get; set; }

	[JsonPropertyName("credentials")]
	public MissionCredentials Credentials { get; set; }

	[JsonPropertyName("flag_target")]
	public MissionFlagTarget FlagTarget { get; set; }
}

public sealed class MissionNetworkDefinition
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("alias")]
	public string Alias { get; set; } = "";
}

public sealed class MissionHostBinding
{
	[JsonPropertyName("host_ip")]
	public string HostIp { get; set; } = "";

	[JsonPropertyName("host_port")]
	public int HostPort { get; set; }

	[JsonPropertyName("container_port")]
	public int ContainerPort { get; set; }
}

public sealed class MissionReadinessDefinition
{
	[JsonPropertyName("type")]
	public string Type { get; set; } = "";

	[JsonPropertyName("from_container")]
	public string FromContainer { get; set; } = "";

	[JsonPropertyName("port")]
	public int Port { get; set; }
}

public sealed class MissionCredentials
{
	[JsonPropertyName("username")]
	public string Username { get; set; } = "";

	[JsonPropertyName("password")]
	public string Password { get; set; } = "";
}

public sealed class MissionFlagTarget
{
	[JsonPropertyName("path")]
	public string Path { get; set; } = "";

	[JsonPropertyName("owner")]
	public string Owner { get; set; } = "";

	[JsonPropertyName("group")]
	public string Group { get; set; } = "";

	[JsonPropertyName("mode")]
	public string Mode { get; set; } = "";
}

internal sealed class MissionInfrastructureCatalogDocument
{
	[JsonPropertyName("infrastructures")]
	public List<MissionInfrastructureDefinition> Infrastructures { get; set; } = new();
}
