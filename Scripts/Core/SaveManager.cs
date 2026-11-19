using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class SaveManager : Node
{
	public static SaveManager Instance { get; private set; }
	public bool IsOfficeWifiConnected { get; private set; }

	private const string SaveFilePath = "user://savegame.json";
	private Dictionary<string, MissionRuntimeSaveData> _missionRuntimeStates =
		new(StringComparer.Ordinal);

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		LoadGame();
	}

	public bool SaveGame()
	{
		if (
			QuestManager.Instance == null ||
			InventoryManager.Instance == null ||
			RewardManager.Instance == null
		)
		{
			return false;
		}

		var data = new SaveGameData
		{
			SchemaVersion = SaveGameData.CurrentSchemaVersion,
			ActiveQuests = QuestManager.Instance.GetActiveQuestStages(),
			CompletedQuests = QuestManager.Instance.GetCompletedQuests(),
			Items = InventoryManager.Instance.GetItemsSnapshot(),
			Experience = InventoryManager.Instance.GetExperience(),
			Credits = InventoryManager.Instance.GetCredits(),
			OfficeWifiConnected = IsOfficeWifiConnected,
			ClaimedQuestRewards =
				RewardManager.Instance.GetClaimedQuestRewards(),
			MissionRuntimeStates = GetMissionRuntimeStatesSnapshot()
		};

		return WriteSaveData(data);
	}

	private bool WriteSaveData(SaveGameData data)
	{
		try
		{
			string json = JsonSerializer.Serialize(
				data,
				new JsonSerializerOptions { WriteIndented = true }
			);

			using var file = FileAccess.Open(
				SaveFilePath,
				FileAccess.ModeFlags.Write
			);

			if (file == null)
			{
				GD.PrintErr("SaveManager: não foi possível abrir o arquivo de salvamento.");
				return false;
			}

			file.StoreString(json);
			return true;
		}
		catch (Exception exception)
		{
			GD.PrintErr($"SaveManager: erro ao salvar o jogo: {exception.Message}");
			return false;
		}
	}

	private void LoadGame()
	{
		if (!FileAccess.FileExists(SaveFilePath))
		{
			GD.Print("SaveManager: nenhum salvamento encontrado. Um novo jogo será iniciado.");
			return;
		}

		try
		{
			using var file = FileAccess.Open(
				SaveFilePath,
				FileAccess.ModeFlags.Read
			);

			if (file == null)
			{
				GD.PrintErr("SaveManager: não foi possível abrir o salvamento.");
				return;
			}

			string json = file.GetAsText();
			var data = JsonSerializer.Deserialize<SaveGameData>(json);

			if (data == null)
			{
				GD.PrintErr("SaveManager: o arquivo de salvamento está vazio ou inválido.");
				return;
			}

			SaveGameMigration.Migrate(data);

			IsOfficeWifiConnected = data.OfficeWifiConnected;
			RestoreMissionRuntimeStates(data.MissionRuntimeStates);

			QuestManager.Instance?.RestoreProgress(
				data.ActiveQuests ?? new Dictionary<string, int>(),
				data.CompletedQuests ?? new List<string>()
			);

			InventoryManager.Instance?.RestoreState(
				data.Items ?? new Dictionary<string, int>(),
				data.Experience,
				data.Credits
			);

			RewardManager.Instance?.RestoreClaimedQuestRewards(
				data.ClaimedQuestRewards ?? new List<string>()
			);

			SaveGame();
			GD.Print("SaveManager: progresso carregado com sucesso.");
		}
		catch (Exception exception)
		{
			GD.PrintErr($"SaveManager: erro ao carregar o jogo: {exception.Message}");
		}
	}

	public void SetOfficeWifiConnected(bool isConnected)
	{
		if (IsOfficeWifiConnected == isConnected)
			return;

		IsOfficeWifiConnected = isConnected;
		SaveGame();
	}

	public bool HasSaveGame()
	{
		return FileAccess.FileExists(SaveFilePath);
	}

	public bool TryGetOrCreateMissionRuntimeState(
		string questId,
		out MissionRuntimeSaveData runtimeState
	)
	{
		runtimeState = null;
		if (string.IsNullOrWhiteSpace(questId))
			return false;

		bool hadPreviousState = _missionRuntimeStates.TryGetValue(
			questId,
			out var previousState
		);

		try
		{
			MissionFlagService.GetOrCreateToken(
				_missionRuntimeStates,
				questId,
				out bool created
			);

			if (created && !SaveGame())
			{
				RestorePreviousRuntimeState(
					questId,
					hadPreviousState,
					previousState
				);
				return false;
			}

			runtimeState = _missionRuntimeStates[questId].Copy();
			return true;
		}
		catch (Exception)
		{
			GD.PrintErr(
				"SaveManager: dados persistentes de missão inválidos."
			);
			return false;
		}
	}

	public bool TryGetMissionRuntimeState(
		string questId,
		out MissionRuntimeSaveData runtimeState
	)
	{
		runtimeState = null;
		if (
			string.IsNullOrWhiteSpace(questId) ||
			!_missionRuntimeStates.TryGetValue(questId, out var storedState) ||
			!MissionFlagService.IsValidToken(storedState?.FlagToken)
		)
		{
			return false;
		}

		runtimeState = storedState.Copy();
		return true;
	}

	public bool RemoveMissionRuntimeState(string questId)
	{
		if (
			string.IsNullOrWhiteSpace(questId) ||
			!_missionRuntimeStates.TryGetValue(questId, out var previousState)
		)
		{
			return true;
		}

		_missionRuntimeStates.Remove(questId);
		if (SaveGame())
			return true;

		_missionRuntimeStates[questId] = previousState;
		return false;
	}

	private Dictionary<string, MissionRuntimeSaveData>
		GetMissionRuntimeStatesSnapshot()
	{
		return _missionRuntimeStates.ToDictionary(
			pair => pair.Key,
			pair => pair.Value?.Copy() ?? new MissionRuntimeSaveData(),
			StringComparer.Ordinal
		);
	}

	public bool ResetProgress()
	{
		var initialData = new SaveGameData
		{
			SchemaVersion = SaveGameData.CurrentSchemaVersion,
			ActiveQuests = new Dictionary<string, int>
			{
				["tutorial"] = 1
			},
			CompletedQuests = new List<string>(),
			Items = new Dictionary<string, int>(),
			Experience = 0,
			Credits = 0,
			OfficeWifiConnected = false,
			ClaimedQuestRewards = new List<string>(),
			MissionRuntimeStates =
				new Dictionary<string, MissionRuntimeSaveData>()
		};

		if (!WriteSaveData(initialData))
		{
			GD.PrintErr(
				"SaveManager: não foi possível gravar o estado inicial."
			);
			return false;
		}

		IsOfficeWifiConnected = false;
		_missionRuntimeStates.Clear();

		QuestManager.Instance?.RestoreProgress(
			initialData.ActiveQuests,
			initialData.CompletedQuests
		);

		InventoryManager.Instance?.RestoreState(
			initialData.Items,
			initialData.Experience,
			initialData.Credits
		);

		RewardManager.Instance?.RestoreClaimedQuestRewards(
			initialData.ClaimedQuestRewards
		);

		GD.Print("SaveManager: progresso reiniciado com sucesso.");
		return true;
	}

	private void RestoreMissionRuntimeStates(
		Dictionary<string, MissionRuntimeSaveData> runtimeStates
	)
	{
		_missionRuntimeStates = (runtimeStates ?? new())
			.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
			.ToDictionary(
				pair => pair.Key,
				pair => pair.Value?.Copy() ?? new MissionRuntimeSaveData(),
				StringComparer.Ordinal
			);
	}

	private void RestorePreviousRuntimeState(
		string questId,
		bool hadPreviousState,
		MissionRuntimeSaveData previousState
	)
	{
		if (hadPreviousState)
			_missionRuntimeStates[questId] = previousState;
		else
			_missionRuntimeStates.Remove(questId);
	}
}
