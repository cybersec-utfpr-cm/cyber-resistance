using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class SaveManager : Node
{
	public static SaveManager Instance { get; private set; }

	private const string SaveFilePath = "user://savegame.json";

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		LoadGame();
	}

	public void SaveGame()
	{
		if (
			QuestManager.Instance == null ||
			InventoryManager.Instance == null ||
			RewardManager.Instance == null
		)
		{
			return;
		}

		try
		{
			var data = new SaveGameData
			{
				ActiveQuests = QuestManager.Instance.GetActiveQuestStages(),
				CompletedQuests = QuestManager.Instance.GetCompletedQuests(),
				Items = InventoryManager.Instance.GetItemsSnapshot(),
				Experience = InventoryManager.Instance.GetExperience(),
				Credits = InventoryManager.Instance.GetCredits(),
				ClaimedQuestRewards =
					RewardManager.Instance.GetClaimedQuestRewards()
			};

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
				return;
			}

			file.StoreString(json);
		}
		catch (Exception exception)
		{
			GD.PrintErr($"SaveManager: erro ao salvar o jogo: {exception.Message}");
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

			GD.Print("SaveManager: progresso carregado com sucesso.");
		}
		catch (Exception exception)
		{
			GD.PrintErr($"SaveManager: erro ao carregar o jogo: {exception.Message}");
		}
	}
}

public class SaveGameData
{
	public Dictionary<string, int> ActiveQuests { get; set; } = new();
	public List<string> CompletedQuests { get; set; } = new();
	public Dictionary<string, int> Items { get; set; } = new();
	public int Experience { get; set; }
	public int Credits { get; set; }
	public List<string> ClaimedQuestRewards { get; set; } = new();
}
