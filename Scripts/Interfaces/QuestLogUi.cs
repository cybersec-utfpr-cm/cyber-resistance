using Godot;
using System.Collections.Generic;

public partial class QuestLogUi : CanvasLayer
{
	[Export] public NodePath QuestListContainerPath { get; set; }
	[Export] public NodePath PanelPath { get; set; }
	[Export] public NodePath ToggleButtonPath { get; set; }
	[Export] public NodePath LevelLabelPath { get; set; }
	[Export] public NodePath ExperienceLabelPath { get; set; }
	[Export] public NodePath ExperienceBarPath { get; set; }
	[Export] public NodePath CreditsLabelPath { get; set; }
	[Export] public bool StartCollapsed { get; set; } = true;

	private const int ExperiencePerLevel = 250;

	private VBoxContainer _questListContainer;
	private Control _root;
	private PanelContainer _panel;
	private Button _toggleButton;
	private Label _levelLabel;
	private Label _experienceLabel;
	private ProgressBar _experienceBar;
	private Label _creditsLabel;
	private bool _isCollapsed;
	private bool _isModalObscured;

	private static readonly Color TitleColor =
		new(0.88f, 0.96f, 0.99f, 1.0f);
	private static readonly Color AccentColor =
		new(0.33f, 0.82f, 0.87f, 1.0f);
	private static readonly Color MutedColor =
		new(0.48f, 0.59f, 0.64f, 1.0f);

	public bool IsCollapsed => _isCollapsed;
	public bool IsModalObscured => _isModalObscured;

	public override void _Ready()
	{
		_root = GetNodeOrNull<Control>("Root");
		_questListContainer = GetNodeOrNull<VBoxContainer>(QuestListContainerPath);
		_panel = GetNodeOrNull<PanelContainer>(PanelPath);
		_toggleButton = GetNodeOrNull<Button>(ToggleButtonPath);
		_levelLabel = GetNodeOrNull<Label>(LevelLabelPath);
		_experienceLabel = GetNodeOrNull<Label>(ExperienceLabelPath);
		_experienceBar = GetNodeOrNull<ProgressBar>(ExperienceBarPath);
		_creditsLabel = GetNodeOrNull<Label>(CreditsLabelPath);

		if (
			_questListContainer == null ||
			_panel == null ||
			_toggleButton == null
		)
		{
			GD.PrintErr(
				"QuestLogUI: estrutura da interface não encontrada. " +
				"Verifique os caminhos configurados na cena."
			);
			return;
		}

		_toggleButton.Pressed += ToggleCollapsed;

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

		if (InventoryManager.Instance != null)
			InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
		else
			GD.PrintErr("QuestLogUI: InventoryManager não encontrado.");

		UpdateQuestList();
		UpdatePlayerHud();
		SetCollapsed(StartCollapsed);
	}

	public override void _ExitTree()
	{
		if (_toggleButton != null)
			_toggleButton.Pressed -= ToggleCollapsed;

		if (QuestManager.Instance != null)
		{
			QuestManager.Instance.QuestStarted -= OnQuestStarted;
			QuestManager.Instance.QuestAdvanced -= OnQuestAdvanced;
			QuestManager.Instance.QuestCompleted -= OnQuestCompleted;
		}

		if (RewardManager.Instance != null)
		{
			RewardManager.Instance.RewardCollected -= OnRewardCollected;
			RewardManager.Instance.RewardClaimFailed -= OnRewardClaimFailed;
		}

		if (InventoryManager.Instance != null)
			InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
	}

	public void SetCollapsed(bool collapsed)
	{
		_isCollapsed = collapsed;

		if (_panel != null)
			_panel.Visible = !collapsed;

		UpdateToggleText();
	}

	public void SetModalObscured(bool obscured)
	{
		_isModalObscured = obscured;

		if (_root != null)
			_root.Visible = !obscured;
	}

	private void ToggleCollapsed()
	{
		SetCollapsed(!_isCollapsed);
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
		GD.Print($"QuestLogUI: missão '{questId}' concluída.");
		UpdateQuestList();
	}

	private void OnRewardCollected(string questId, string rewardId)
	{
		GD.Print($"QuestLogUI: recompensa coletada da missão '{questId}'.");
		UpdateQuestList();
		UpdatePlayerHud();
	}

	private void OnRewardClaimFailed(string questId, string reason)
	{
		GD.PrintErr($"QuestLogUI: não foi possível coletar recompensa da missão '{questId}': {reason}");
	}

	private void OnInventoryChanged()
	{
		UpdatePlayerHud();
	}

	private void UpdatePlayerHud()
	{
		if (InventoryManager.Instance == null)
			return;

		int experience = InventoryManager.Instance.GetExperience();
		int level = (experience / ExperiencePerLevel) + 1;
		int levelExperience = experience % ExperiencePerLevel;

		if (_levelLabel != null)
			_levelLabel.Text = $"NÍVEL {level}";

		if (_experienceLabel != null)
			_experienceLabel.Text = $"XP {experience}";

		if (_experienceBar != null)
		{
			_experienceBar.MaxValue = ExperiencePerLevel;
			_experienceBar.Value = levelExperience;
			_experienceBar.TooltipText =
				$"{levelExperience}/{ExperiencePerLevel} XP para o próximo nível";
		}

		if (_creditsLabel != null)
			_creditsLabel.Text = $"MOEDAS {InventoryManager.Instance.GetCredits()}";
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

		UpdateToggleText();
	}

	private void ClearQuestList()
	{
		foreach (Node child in _questListContainer.GetChildren())
		{
			_questListContainer.RemoveChild(child);
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

		var entryPanel = new PanelContainer();
		entryPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var entryMargin = CreateMarginContainer(7);
		var entryContent = new VBoxContainer();
		entryContent.AddThemeConstantOverride("separation", 4);

		var titleLabel = new Label();
		titleLabel.Text = def.Title;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		titleLabel.AddThemeFontSizeOverride("font_size", 13);
		entryContent.AddChild(titleLabel);

		var descriptionLabel = new Label();
		descriptionLabel.Text = description;
		descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descriptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		descriptionLabel.AddThemeColorOverride("font_color", MutedColor);
		descriptionLabel.AddThemeFontSizeOverride("font_size", 11);
		entryContent.AddChild(descriptionLabel);

		entryMargin.AddChild(entryContent);
		entryPanel.AddChild(entryMargin);
		_questListContainer.AddChild(entryPanel);
	}

	private void AddRewardEntry(string questId)
	{
		var quest = QuestManager.Instance.GetQuestDefinition(questId);
		var reward = RewardManager.Instance.GetQuestReward(questId);

		if (quest == null || reward == null)
			return;

		var entryPanel = new PanelContainer();
		entryPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var entryMargin = CreateMarginContainer(7);
		var entryContent = new VBoxContainer();
		entryContent.AddThemeConstantOverride("separation", 5);

		var titleLabel = new Label();
		titleLabel.Text = $"{quest.Title}: {reward.Title}";
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		titleLabel.AddThemeFontSizeOverride("font_size", 13);
		entryContent.AddChild(titleLabel);

		var descriptionLabel = new Label();
		descriptionLabel.Text = RewardManager.Instance.GetRewardSummary(questId);
		descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descriptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		descriptionLabel.AddThemeColorOverride("font_color", MutedColor);
		descriptionLabel.AddThemeFontSizeOverride("font_size", 11);
		entryContent.AddChild(descriptionLabel);

		var collectButton = new Button();
		collectButton.Text = "Coletar recompensa";
		collectButton.CustomMinimumSize = new Vector2(0, 34);
		collectButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		collectButton.AddThemeFontSizeOverride("font_size", 12);

		collectButton.Pressed += () =>
		{
			GD.Print($"QuestLogUI: tentando coletar recompensa da missão '{questId}'.");
			RewardManager.Instance.CollectQuestReward(questId);
		};

		entryContent.AddChild(collectButton);

		entryMargin.AddChild(entryContent);
		entryPanel.AddChild(entryMargin);
		_questListContainer.AddChild(entryPanel);
	}

	private void AddSectionTitle(string text)
	{
		var label = new Label();
		label.Text = text;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.AddThemeColorOverride("font_color", AccentColor);
		label.AddThemeFontSizeOverride("font_size", 13);
		_questListContainer.AddChild(label);
	}

	private void AddMutedLabel(string text)
	{
		var label = new Label();
		label.Text = text;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.AddThemeColorOverride("font_color", MutedColor);
		label.AddThemeFontSizeOverride("font_size", 11);
		_questListContainer.AddChild(label);
	}

	private void AddSeparator()
	{
		var separator = new HSeparator();
		separator.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_questListContainer.AddChild(separator);
	}

	private MarginContainer CreateMarginContainer(int margin)
	{
		var container = new MarginContainer();
		container.AddThemeConstantOverride("margin_left", margin);
		container.AddThemeConstantOverride("margin_top", margin);
		container.AddThemeConstantOverride("margin_right", margin);
		container.AddThemeConstantOverride("margin_bottom", margin);
		return container;
	}

	private void UpdateToggleText()
	{
		if (_toggleButton == null)
			return;

		int activeCount =
			QuestManager.Instance?.GetActiveQuests().Count ?? 0;
		int rewardCount =
			RewardManager.Instance?.GetClaimableQuestRewardIds().Count ?? 0;
		int totalCount = activeCount + rewardCount;

		string indicator = _isCollapsed ? "▸" : "▾";
		string count = totalCount > 0 ? $" ({totalCount})" : "";

		_toggleButton.Text = $"{indicator}  Missões{count}";
	}
}
