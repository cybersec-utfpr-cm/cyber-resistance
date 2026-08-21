using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionInfrastructureManager : Node
{
	private const string CatalogPath = "res://Data/missionInfrastructure.json";
	private const int ReadinessAttempts = 10;
	private static readonly TimeSpan ReadinessDelay = TimeSpan.FromMilliseconds(500);

	private readonly SemaphoreSlim _operationLock = new(1, 1);
	private readonly CancellationTokenSource _lifetimeCancellation = new();
	private readonly Dictionary<string, MissionLabState> _missionStates = new(
		StringComparer.Ordinal
	);

	private MissionInfrastructureCatalog _catalog;
	private DockerManager _docker;
	private MissionInfrastructureDefinition _playerDefinition;

	public static MissionInfrastructureManager Instance { get; private set; }

	[Signal]
	public delegate void MissionStateChangedEventHandler(string questId);

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override async void _Ready()
	{
		try
		{
			LoadCatalog();
			SubscribeToQuestEvents();
			await InitializeAsync(_lifetimeCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			// The autoload is leaving the tree.
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				"MissionInfrastructureManager: falha na inicialização: " +
				exception.Message
			);
		}
	}

	public override void _ExitTree()
	{
		_lifetimeCancellation.Cancel();
		UnsubscribeFromQuestEvents();

		if (Instance == this)
			Instance = null;
	}

	public Task<bool> InitializeAsync(
		CancellationToken cancellationToken = default
	)
	{
		return ReconcileAsync(cancellationToken);
	}

	public async Task<bool> ReconcileAsync(
		CancellationToken cancellationToken = default
	)
	{
		if (!IsCatalogReady())
			return false;

		using CancellationTokenSource operationCancellation =
			CreateOperationCancellation(cancellationToken);
		await _operationLock.WaitAsync(operationCancellation.Token);

		try
		{
			var activeInfrastructure = GetActiveMissionInfrastructure();
			var runtimeReady = new Dictionary<string, bool>(StringComparer.Ordinal);

			foreach (var activeMission in activeInfrastructure)
			{
				MissionInfrastructureDefinition mission =
					_catalog.GetDefinition(activeMission.Key);
				bool isRuntimeReady = EnsureMissionRuntimeState(
					activeMission.Value,
					mission
				);
				runtimeReady[mission.Id] = isRuntimeReady;

				if (!isRuntimeReady)
				{
					SetMissionFailed(
						activeMission.Value,
						mission,
						"Não foi possível persistir os dados da missão. " +
							"Carregue o jogo e tente novamente."
					);
				}
			}

			bool playerReady = await EnsurePlayerMachineReadyInternalAsync(
				operationCancellation.Token
			);

			foreach (
				MissionInfrastructureDefinition mission in
				_catalog.Definitions.Where(
					definition =>
						definition.Kind == MissionInfrastructureKind.Mission
				)
			)
			{
				if (
					activeInfrastructure.TryGetValue(
						mission.Id,
						out string questId
					)
				)
				{
					if (!runtimeReady.GetValueOrDefault(mission.Id))
						continue;

					if (playerReady)
					{
						await PrepareMissionInternalAsync(
							questId,
							mission,
							operationCancellation.Token
						);
					}
					else
					{
						SetMissionFailed(
							questId,
							mission,
							"O computador do jogador não ficou pronto."
						);
					}
				}
				else
				{
					await _docker.StopContainerAsync(
						mission.Id,
						operationCancellation.Token
					);
					RemoveStatesForInfrastructure(mission.Id);
				}
			}

			return playerReady && ActiveMissionStatesAreReady();
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				"MissionInfrastructureManager: falha na reconciliação: " +
				exception.Message
			);
			return false;
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public async Task<bool> EnsurePlayerMachineReadyAsync(
		CancellationToken cancellationToken = default
	)
	{
		if (!IsCatalogReady())
			return false;

		using CancellationTokenSource operationCancellation =
			CreateOperationCancellation(cancellationToken);
		await _operationLock.WaitAsync(operationCancellation.Token);

		try
		{
			return await EnsurePlayerMachineReadyInternalAsync(
				operationCancellation.Token
			);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				"MissionInfrastructureManager: falha ao preparar player: " +
				exception.Message
			);
			return false;
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public async Task<bool> PrepareMissionAsync(
		string questId,
		CancellationToken cancellationToken = default
	)
	{
		if (!TryGetActiveMissionDefinition(questId, out var mission))
			return false;

		using CancellationTokenSource operationCancellation =
			CreateOperationCancellation(cancellationToken);
		await _operationLock.WaitAsync(operationCancellation.Token);

		try
		{
			if (!EnsureMissionRuntimeState(questId, mission))
			{
				SetMissionFailed(
					questId,
					mission,
					"Não foi possível persistir os dados da missão. " +
						"Carregue o jogo e tente novamente."
				);
				return false;
			}

			if (
				!await EnsurePlayerMachineReadyInternalAsync(
					operationCancellation.Token
				)
			)
			{
				SetMissionFailed(
					questId,
					mission,
					"O computador do jogador não ficou pronto."
				);
				return false;
			}

			return await PrepareMissionInternalAsync(
				questId,
				mission,
				operationCancellation.Token
			);
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public Task<bool> RetryMissionAsync(
		string questId,
		CancellationToken cancellationToken = default
	)
	{
		return PrepareMissionAsync(questId, cancellationToken);
	}

	public async void HandleMissionRetryRequested(string questId)
	{
		try
		{
			await RetryMissionAsync(questId, _lifetimeCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			// The application is closing or a newer lifecycle operation won.
		}
		catch (Exception)
		{
			GD.PrintErr(
				"MissionInfrastructureManager: falha inesperada ao repetir missão."
			);
		}
	}

	public async Task<bool> CompleteMissionAsync(
		string questId,
		CancellationToken cancellationToken = default
	)
	{
		QuestDefinition quest = QuestManager.Instance?.GetQuestDefinition(questId);
		if (
			quest == null ||
			string.IsNullOrWhiteSpace(quest.InfrastructureId) ||
			!IsCatalogReady()
		)
		{
			return false;
		}

		MissionInfrastructureDefinition mission;
		try
		{
			mission = _catalog.GetDefinition(quest.InfrastructureId);
		}
		catch (ArgumentException)
		{
			return false;
		}

		using CancellationTokenSource operationCancellation =
			CreateOperationCancellation(cancellationToken);
		await _operationLock.WaitAsync(operationCancellation.Token);

		try
		{
			await _docker.StopContainerAsync(
				mission.Id,
				operationCancellation.Token
			);

			if (
				SaveManager.Instance != null &&
				!SaveManager.Instance.RemoveMissionRuntimeState(questId)
			)
			{
				GD.PrintErr(
					"MissionInfrastructureManager: não foi possível remover " +
					"os dados persistentes da missão concluída."
				);
				return false;
			}

			RemoveMissionState(questId);
			return true;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				$"MissionInfrastructureManager: falha ao concluir infraestrutura " +
				$"de '{questId}': {exception.Message}"
			);
			return false;
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public async Task<bool> ShutdownAsync(
		CancellationToken cancellationToken = default
	)
	{
		if (!IsCatalogReady())
			return false;

		await _operationLock.WaitAsync(cancellationToken);
		bool succeeded = true;

		try
		{
			foreach (
				MissionInfrastructureDefinition mission in
				_catalog.Definitions.Where(
					definition =>
						definition.Kind == MissionInfrastructureKind.Mission
				)
			)
			{
				succeeded &= await TryStopContainerAsync(
					mission.Id,
					cancellationToken
				);
			}

			succeeded &= await TryStopContainerAsync(
				_playerDefinition.Id,
				cancellationToken
			);
			return succeeded;
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public async Task<bool> ResetForNewGameAsync(
		CancellationToken cancellationToken = default
	)
	{
		if (!IsCatalogReady())
			return false;

		await _operationLock.WaitAsync(cancellationToken);

		try
		{
			foreach (
				MissionInfrastructureDefinition mission in
				_catalog.Definitions.Where(
					definition =>
						definition.Kind == MissionInfrastructureKind.Mission
				)
			)
			{
				await _docker.RemoveContainerAsync(mission.Id, cancellationToken);
			}

			await _docker.RemoveContainerAsync(
				_playerDefinition.Id,
				cancellationToken
			);

			foreach (string networkName in _catalog.OwnedNetworks)
				await _docker.RemoveNetworkAsync(networkName, cancellationToken);

			foreach (string questId in _missionStates.Keys.ToList())
				RemoveMissionState(questId);

			return await EnsurePlayerMachineReadyInternalAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				"MissionInfrastructureManager: falha no reset Docker: " +
				exception.Message
			);
			return false;
		}
		finally
		{
			_operationLock.Release();
		}
	}

	public MissionLabState GetMissionState(string questId)
	{
		return _missionStates.TryGetValue(questId, out var state)
			? state.Copy()
			: null;
	}

	private void LoadCatalog()
	{
		using FileAccess file = FileAccess.Open(CatalogPath, FileAccess.ModeFlags.Read);
		if (file == null)
			throw new InvalidOperationException("Catálogo de infraestrutura não encontrado.");

		_catalog = MissionInfrastructureCatalog.Parse(file.GetAsText());
		_playerDefinition = _catalog.Definitions.Single(
			definition => definition.Kind == MissionInfrastructureKind.Player
		);
		_docker = new DockerManager(_catalog);
	}

	private async Task<bool> EnsurePlayerMachineReadyInternalAsync(
		CancellationToken cancellationToken
	)
	{
		try
		{
			await _docker.StartContainerAsync(_playerDefinition.Id, cancellationToken);

			for (int attempt = 0; attempt < ReadinessAttempts; attempt++)
			{
				if (
					await _docker.ProbePlayerReadinessAsync(
						_playerDefinition.Id,
						cancellationToken
					)
				)
				{
					return true;
				}

				if (attempt + 1 < ReadinessAttempts)
					await Task.Delay(ReadinessDelay, cancellationToken);
			}

			GD.PrintErr(
				"MissionInfrastructureManager: o serviço do player não ficou pronto."
			);
			return false;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				"MissionInfrastructureManager: falha ao iniciar player: " +
				exception.Message
			);
			return false;
		}
	}

	private async Task<bool> PrepareMissionInternalAsync(
		string questId,
		MissionInfrastructureDefinition mission,
		CancellationToken cancellationToken
	)
	{
		SetMissionState(
			CreateMissionState(questId, mission, MissionLabStatus.Preparing)
		);

		try
		{
			MissionRuntimeSaveData runtimeState = null;
			if (
				mission.FlagTarget != null &&
				(
					SaveManager.Instance == null ||
					!SaveManager.Instance.TryGetMissionRuntimeState(
						questId,
						out runtimeState
					)
				)
			)
			{
				SetMissionFailed(
					questId,
					mission,
					"Os dados persistentes da missão não estão disponíveis. " +
						"Carregue o jogo e tente novamente."
				);
				return false;
			}

			await _docker.EnsureNetworkCreatedAsync(
				mission.Network.Name,
				cancellationToken
			);
			await _docker.StartContainerAsync(mission.Id, cancellationToken);
			await _docker.ConnectNetworkAsync(
				_playerDefinition.Id,
				mission.Network.Name,
				_playerDefinition.NetworkAlias,
				cancellationToken
			);
			await _docker.ConnectNetworkAsync(
				mission.Id,
				mission.Network.Name,
				mission.Network.Alias,
				cancellationToken
			);

			bool ready = false;
			for (int attempt = 0; attempt < ReadinessAttempts; attempt++)
			{
				ready = await _docker.ProbeMissionReadinessAsync(
					mission.Id,
					cancellationToken
				);
				if (ready)
					break;

				if (attempt + 1 < ReadinessAttempts)
					await Task.Delay(ReadinessDelay, cancellationToken);
			}

			if (!ready)
			{
				SetMissionFailed(
					questId,
					mission,
					"O serviço SSH do laboratório não respondeu. Tente novamente."
				);
				return false;
			}

			if (mission.FlagTarget != null)
			{
				await InjectMissionFlagAsync(
					mission,
					runtimeState.FlagToken,
					cancellationToken
				);
			}

			string internalIp = await _docker.ResolveContainerIpAsync(
				mission.Id,
				mission.Network.Name,
				cancellationToken
			);
			MissionLabState state = CreateMissionState(
				questId,
				mission,
				MissionLabStatus.Ready
			);
			state.InternalIp = internalIp;
			SetMissionState(state);
			return true;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			SetMissionFailed(questId, mission, exception.Message);
			return false;
		}
	}

	private bool EnsureMissionRuntimeState(
		string questId,
		MissionInfrastructureDefinition mission
	)
	{
		if (mission.FlagTarget == null)
			return true;

		return
			SaveManager.Instance != null &&
			SaveManager.Instance.TryGetOrCreateMissionRuntimeState(
				questId,
				out _
			);
	}

	private async Task InjectMissionFlagAsync(
		MissionInfrastructureDefinition mission,
		string flagToken,
		CancellationToken cancellationToken
	)
	{
		MissionFlagTarget target = mission.FlagTarget;
		string fileContent = MissionFlagService.BuildFileContent(flagToken);

		await _docker.ExecuteAsync(
			mission.Id,
			MissionFlagService.BuildInstallCommand(target),
			fileContent,
			cancellationToken
		);
	}

	private bool TryGetActiveMissionDefinition(
		string questId,
		out MissionInfrastructureDefinition mission
	)
	{
		mission = null;
		if (
			!IsCatalogReady() ||
			QuestManager.Instance == null ||
			!QuestManager.Instance.IsQuestActive(questId)
		)
		{
			return false;
		}

		QuestDefinition quest = QuestManager.Instance.GetQuestDefinition(questId);
		if (quest == null || string.IsNullOrWhiteSpace(quest.InfrastructureId))
			return false;

		try
		{
			mission = _catalog.GetDefinition(quest.InfrastructureId);
			return mission.Kind == MissionInfrastructureKind.Mission;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	private Dictionary<string, string> GetActiveMissionInfrastructure()
	{
		var activeInfrastructure = new Dictionary<string, string>(StringComparer.Ordinal);
		if (QuestManager.Instance == null)
			return activeInfrastructure;

		foreach (string questId in QuestManager.Instance.GetActiveQuests())
		{
			QuestDefinition quest = QuestManager.Instance.GetQuestDefinition(questId);
			if (quest != null && !string.IsNullOrWhiteSpace(quest.InfrastructureId))
				activeInfrastructure[quest.InfrastructureId] = questId;
		}

		return activeInfrastructure;
	}

	private bool ActiveMissionStatesAreReady()
	{
		foreach (string questId in GetActiveMissionInfrastructure().Values)
		{
			if (
				!_missionStates.TryGetValue(questId, out var state) ||
				state.Status != MissionLabStatus.Ready
			)
			{
				return false;
			}
		}

		return true;
	}

	private MissionLabState CreateMissionState(
		string questId,
		MissionInfrastructureDefinition mission,
		MissionLabStatus status
	)
	{
		return new MissionLabState
		{
			QuestId = questId,
			InfrastructureId = mission.Id,
			Status = status,
			Username = mission.Credentials?.Username ?? "",
			Password = mission.Credentials?.Password ?? ""
		};
	}

	private void SetMissionFailed(
		string questId,
		MissionInfrastructureDefinition mission,
		string errorMessage
	)
	{
		MissionLabState state = CreateMissionState(
			questId,
			mission,
			MissionLabStatus.Failed
		);
		state.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
			? "Não foi possível preparar o laboratório. Tente novamente."
			: errorMessage;
		SetMissionState(state);
	}

	private void SetMissionState(MissionLabState state)
	{
		_missionStates[state.QuestId] = state;
		NotifyMissionStateChanged(state.QuestId);
	}

	private void RemoveStatesForInfrastructure(string infrastructureId)
	{
		foreach (
			string questId in _missionStates
				.Where(pair => pair.Value.InfrastructureId == infrastructureId)
				.Select(pair => pair.Key)
				.ToList()
		)
		{
			RemoveMissionState(questId);
		}
	}

	private void RemoveMissionState(string questId)
	{
		if (_missionStates.Remove(questId))
			NotifyMissionStateChanged(questId);
	}

	private void NotifyMissionStateChanged(string questId)
	{
		CallDeferred(nameof(EmitMissionStateChanged), questId);
	}

	private void EmitMissionStateChanged(string questId)
	{
		EmitSignal(SignalName.MissionStateChanged, questId);
	}

	private async Task<bool> TryStopContainerAsync(
		string infrastructureId,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await _docker.StopContainerAsync(infrastructureId, cancellationToken);
			return true;
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				$"MissionInfrastructureManager: falha ao parar " +
				$"'{infrastructureId}': {exception.Message}"
			);
			return false;
		}
	}

	private void SubscribeToQuestEvents()
	{
		if (QuestManager.Instance == null)
			return;

		QuestManager.Instance.QuestStarted += OnQuestStarted;
		QuestManager.Instance.QuestCompleted += OnQuestCompleted;
	}

	private void UnsubscribeFromQuestEvents()
	{
		if (QuestManager.Instance == null)
			return;

		QuestManager.Instance.QuestStarted -= OnQuestStarted;
		QuestManager.Instance.QuestCompleted -= OnQuestCompleted;
	}

	private async void OnQuestStarted(string questId)
	{
		try
		{
			await PrepareMissionAsync(questId, _lifetimeCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			// The autoload is leaving the tree.
		}
	}

	private async void OnQuestCompleted(string questId)
	{
		try
		{
			await CompleteMissionAsync(questId, _lifetimeCancellation.Token);
		}
		catch (OperationCanceledException)
		{
			// The autoload is leaving the tree.
		}
	}

	private bool IsCatalogReady()
	{
		return _catalog != null && _docker != null && _playerDefinition != null;
	}

	private CancellationTokenSource CreateOperationCancellation(
		CancellationToken cancellationToken
	)
	{
		return CancellationTokenSource.CreateLinkedTokenSource(
			_lifetimeCancellation.Token,
			cancellationToken
		);
	}
}

public enum MissionLabStatus
{
	Inactive,
	Preparing,
	Ready,
	Failed
}

public sealed class MissionLabState
{
	public string QuestId { get; set; } = "";
	public string InfrastructureId { get; set; } = "";
	public MissionLabStatus Status { get; set; } = MissionLabStatus.Inactive;
	public string InternalIp { get; set; } = "";
	public string ErrorMessage { get; set; } = "";
	public string Username { get; set; } = "";
	public string Password { get; set; } = "";

	public MissionLabState Copy()
	{
		return new MissionLabState
		{
			QuestId = QuestId,
			InfrastructureId = InfrastructureId,
			Status = Status,
			InternalIp = InternalIp,
			ErrorMessage = ErrorMessage,
			Username = Username,
			Password = Password
		};
	}
}
