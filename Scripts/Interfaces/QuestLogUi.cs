using Godot;
using System.Collections.Generic;

public partial class QuestLogUi : CanvasLayer
{
	[Export] public NodePath QuestListContainerPath { get; set; }

	private VBoxContainer _questListContainer;

	public override void _Ready()
	{
		_questListContainer = GetNodeOrNull<VBoxContainer>(QuestListContainerPath);

		if (_questListContainer == null)
		{
			GD.PrintErr("QuestLogUI: QuestListContainer não encontrado. Verifique o campo QuestListContainerPath no Inspector.");
			return;
		}

		if (QuestManager.Instance != null)
		{
			QuestManager.Instance.QuestStarted += OnQuestStarted;
			QuestManager.Instance.QuestAdvanced += OnQuestAdvanced;
			QuestManager.Instance.QuestCompleted += OnQuestCompleted;
		}
		else
		{
			GD.PrintErr("QuestLogUI: QuestManager não encontrado.");
		}

		if (RewardManager.Instance != null)
		{
			RewardManager.Instance.RewardCollected += OnRewardCollected;
			RewardManager.Instance.RewardClaimFailed += OnRewardClaimFailed;
		}
		else
		{
			GD.PrintErr("QuestLogUI: RewardManager não encontrado.");
		}

		UpdateQuestList();
	}

	private void OnQuestStarted(string questId)
	{
		UpdateQuestList();
	}

	private void OnQuestAdvanced(string questId, int newStage)
	{
		UpdateQuestList();
	}

	private void OnQuestCompleted(string questId)
	{
		if (questId == "tutorial")
		{
			QuestManager.Instance.StartQuest("wifi_hacking");
			GD.Print("Missão wifi_hacking iniciada após conclusão do tutorial.");
		}

		if (questId == "wifi_hacking")
		{
			QuestManager.Instance.StartQuest("university_exam");
			GD.Print("Missão university_exam iniciada.");
		}

		UpdateQuestList();
	}

	private void OnRewardCollected(string questId, string rewardId)
	{
		GD.Print($"QuestLogUI: recompensa coletada da missão '{questId}'.");
		UpdateQuestList();
	}

	private void OnRewardClaimFailed(string questId, string reason)
	{
		GD.PrintErr($"QuestLogUI: não foi possível coletar recompensa da missão '{questId}': {reason}");
	}

	private void UpdateQuestList()
	{
		if (_questListContainer == null)
			return;

		ClearQuestList();

		GD.Print("QuestLogUI: atualizando lista de missões e recompensas.");

		AddSectionTitle("Missões ativas");

		var activeQuests = QuestManager.Instance?.GetActiveQuests() ?? new List<string>();

		if (activeQuests.Count == 0)
		{
			AddMutedLabel("Nenhuma missão ativa.");
		}
		else
		{
			foreach (var questId in activeQuests)
			{
				AddActiveQuestEntry(questId);
			}
		}

		AddSeparator();

		AddSectionTitle("Recompensas disponíveis");

		var claimableRewards = RewardManager.Instance?.GetClaimableQuestRewardIds() ?? new List<string>();

		GD.Print($"QuestLogUI: recompensas disponíveis: {claimableRewards.Count}");

		if (claimableRewards.Count == 0)
		{
			AddMutedLabel("Nenhuma recompensa para coletar.");
		}
		else
		{
			foreach (var questId in claimableRewards)
			{
				AddRewardEntry(questId);
			}
		}
	}

	private void ClearQuestList()
	{
		foreach (Node child in _questListContainer.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void AddActiveQuestEntry(string questId)
	{
		var def = QuestManager.Instance.GetQuestDefinition(questId);
		if (def == null)
			return;

		int stage = QuestManager.Instance.GetQuestStage(questId);

		string description = "";

		if (stage > 0 && stage <= def.Stages.Count)
			description = def.Stages[stage - 1].Description;
		else if (stage > def.Stages.Count)
			description = "Concluída";

		var titleLabel = new Label();
		titleLabel.Text = $"• {def.Title}";
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(titleLabel);

		var descriptionLabel = new Label();
		descriptionLabel.Text = description;
		descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descriptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(descriptionLabel);
	}

	private void AddRewardEntry(string questId)
	{
		var quest = QuestManager.Instance.GetQuestDefinition(questId);
		var reward = RewardManager.Instance.GetQuestReward(questId);

		if (quest == null || reward == null)
			return;

		var titleLabel = new Label();
		titleLabel.Text = $"{quest.Title}: {reward.Title}";
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(titleLabel);

		var descriptionLabel = new Label();
		descriptionLabel.Text = RewardManager.Instance.GetRewardSummary(questId);
		descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descriptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(descriptionLabel);

		var collectButton = new Button();
		collectButton.Text = "Coletar recompensa";
		collectButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		collectButton.Pressed += () =>
		{
			GD.Print($"QuestLogUI: tentando coletar recompensa da missão '{questId}'.");
			RewardManager.Instance.CollectQuestReward(questId);
		};

		_questListContainer.AddChild(collectButton);

		AddSeparator();
	}

	private void AddSectionTitle(string text)
	{
		var label = new Label();
		label.Text = text;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(label);
	}

	private void AddMutedLabel(string text)
	{
		var label = new Label();
		label.Text = text;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(label);
	}

	private void AddSeparator()
	{
		var separator = new HSeparator();
		separator.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(separator);
	}
}	
