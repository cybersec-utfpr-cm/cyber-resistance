using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RewardManager : Node
{
	public static RewardManager Instance { get; private set; }

	private readonly Dictionary<string, RewardDefinition> _rewardDefinitions = new();
	private readonly HashSet<string> _claimedQuestRewards = new();

	[Signal] public delegate void RewardCollectedEventHandler(string questId, string rewardId);
	[Signal] public delegate void RewardClaimFailedEventHandler(string questId, string reason);


	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		GD.Print("RewardManager: iniciado.");
		LoadRewards();
	}

	private void LoadRewards()
	{
		const string path = "res://Data/rewards.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

		if (file == null)
		{
			GD.PrintErr($"RewardManager: arquivo não encontrado: {path}");
			return;
		}

		string content = file.GetAsText();
		var json = new Json();
		var result = json.Parse(content);

		if (result != Error.Ok)
		{
			GD.PrintErr($"RewardManager: erro ao ler JSON em {path}: {json.GetErrorMessage()} na linha {json.GetErrorLine()}");
			return;
		}

		var data = json.Data.AsGodotDictionary();
		if (!data.ContainsKey("rewards"))
		{
			GD.PrintErr("RewardManager: rewards.json precisa conter a chave 'rewards'.");
			return;
		}

		foreach (var rewardVar in data["rewards"].AsGodotArray())
		{
			var rewardDict = rewardVar.AsGodotDictionary();
			string id = rewardDict["id"].AsString();

			var reward = new RewardDefinition
			{
				Id = id,
				Title = rewardDict.ContainsKey("title") ? rewardDict["title"].AsString() : id,
				Description = rewardDict.ContainsKey("description") ? rewardDict["description"].AsString() : "",
				Xp = rewardDict.ContainsKey("xp") ? rewardDict["xp"].AsInt32() : 0,
				Credits = rewardDict.ContainsKey("credits") ? rewardDict["credits"].AsInt32() : 0
			};

			if (rewardDict.ContainsKey("items"))
			{
				foreach (var itemVar in rewardDict["items"].AsGodotArray())
				{
					var itemDict = itemVar.AsGodotDictionary();
					reward.Items.Add(new RewardItemEntry
					{
						Id = itemDict.ContainsKey("id") ? itemDict["id"].AsString() : "",
						Name = itemDict.ContainsKey("name") ? itemDict["name"].AsString() : "Item sem nome",
						Amount = itemDict.ContainsKey("amount") ? itemDict["amount"].AsInt32() : 1
					});
				}
			}

			_rewardDefinitions[id] = reward;
		}

		GD.Print($"RewardManager: carregadas {_rewardDefinitions.Count} recompensas.");
	}

	public RewardDefinition GetQuestReward(string questId)
	{
		var quest = QuestManager.Instance?.GetQuestDefinition(questId);
		if (quest == null || string.IsNullOrWhiteSpace(quest.RewardId))
			return null;

		return _rewardDefinitions.TryGetValue(quest.RewardId, out var reward) ? reward : null;
	}

	public bool CanCollectQuestReward(string questId)
	{
		if (QuestManager.Instance == null || !QuestManager.Instance.IsQuestCompleted(questId))
			return false;

		if (_claimedQuestRewards.Contains(questId))
			return false;

		return GetQuestReward(questId) != null;
	}

	public List<string> GetClaimableQuestRewardIds()
	{
		if (QuestManager.Instance == null)
			return new List<string>();

		return QuestManager.Instance.GetCompletedQuests()
			.Where(CanCollectQuestReward)
			.ToList();
	}

	public bool CollectQuestReward(string questId)
	{
		if (!CanCollectQuestReward(questId))
		{
			EmitSignal(SignalName.RewardClaimFailed, questId, "A recompensa não está disponível.");
			return false;
		}

		var reward = GetQuestReward(questId);
		ApplyReward(reward);

		_claimedQuestRewards.Add(questId);
		GD.Print($"RewardManager: recompensa '{reward.Id}' coletada para a missão '{questId}'.");

		EmitSignal(SignalName.RewardCollected, questId, reward.Id);
		SaveManager.Instance?.SaveGame();
		return true;
	}

	private void ApplyReward(RewardDefinition reward)
	{
		if (InventoryManager.Instance == null)
		{
			GD.PrintErr("RewardManager: InventoryManager não encontrado.");
			return;
		}

		if (reward.Xp > 0)
			InventoryManager.Instance.AddExperience(reward.Xp);

		if (reward.Credits > 0)
			InventoryManager.Instance.AddCredits(reward.Credits);

		foreach (var item in reward.Items)
		{
			if (!string.IsNullOrWhiteSpace(item.Id) && item.Amount > 0)
				InventoryManager.Instance.AddItem(item.Id, item.Amount);
		}
	}

	public string GetRewardSummary(string questId)
	{
		var reward = GetQuestReward(questId);
		if (reward == null)
			return "Sem recompensa configurada.";

		var parts = new List<string>();

		if (reward.Xp > 0)
			parts.Add($"{reward.Xp} XP");

		if (reward.Credits > 0)
			parts.Add($"{reward.Credits} créditos");

		parts.AddRange(reward.Items
			.Where(i => i.Amount > 0)
			.Select(i => $"{i.Amount}x {i.Name}"));

		return parts.Count == 0 ? "Recompensa vazia." : string.Join(" | ", parts);
	}

	public List<string> GetClaimedQuestRewards()
	{
		return _claimedQuestRewards.ToList();
	}

	public void RestoreClaimedQuestRewards(IEnumerable<string> questIds)
	{
		_claimedQuestRewards.Clear();

		if (questIds == null)
			return;

		foreach (var questId in questIds)
		{
			if (!string.IsNullOrWhiteSpace(questId))
				_claimedQuestRewards.Add(questId);
		}
	}
}
