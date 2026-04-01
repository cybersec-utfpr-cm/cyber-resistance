using Godot;
using System.Collections.Generic;

public partial class QuestLogUi : CanvasLayer
{
	[Export] public NodePath QuestListContainerPath { get; set; }
	private VBoxContainer _questListContainer;

	private Dictionary<string, (Button button, Label descriptionLabel)> _questEntries = new();

	public override void _Ready()
	{
		_questListContainer = GetNode<VBoxContainer>(QuestListContainerPath);
		if (_questListContainer == null)
		{
			GD.PrintErr("QuestLogUI: QuestListContainer não encontrado.");
			return;
		}

		// Conecta aos sinais do QuestManager
		QuestManager.Instance.QuestStarted += OnQuestStarted;
		QuestManager.Instance.QuestAdvanced += OnQuestAdvanced;
		QuestManager.Instance.QuestCompleted += OnQuestCompleted;

		UpdateQuestList(); // Preenche a lista inicial
	}

	private void OnQuestStarted(string questId)
	{
		AddQuestButton(questId);
	}

	private void OnQuestAdvanced(string questId, int newStage)
	{
		if (_questEntries.TryGetValue(questId, out var entry))
		{
			// Atualiza o texto da descrição
			var def = QuestManager.Instance.GetQuestDefinition(questId);
			if (def != null)
			{
				int stage = QuestManager.Instance.GetQuestStage(questId);
				string description = "";
				if (stage > 0 && stage <= def.Stages.Count)
					description = def.Stages[stage - 1].Description;
				else if (stage > def.Stages.Count)
					description = "Concluída";
				entry.descriptionLabel.Text = description;
			}
		}
		else
		{
			// Se por algum motivo a missão não estiver na lista, adiciona
			AddQuestButton(questId);
		}
	}

	private void OnQuestCompleted(string questId)
	{
		if (_questEntries.TryGetValue(questId, out var entry))
		{
			_questListContainer.RemoveChild(entry.button);
			_questListContainer.RemoveChild(entry.descriptionLabel);
			entry.button.QueueFree();
			entry.descriptionLabel.QueueFree();
			_questEntries.Remove(questId);
		}
		if (questId == "tutorial")
		{
		QuestManager.Instance.StartQuest("wifi_hacking");
		GD.Print("Missão wifi_hacking iniciada após conclusão do tutorial.");
		}
	}

	private void UpdateQuestList()
	{
		// Remove todos os itens atuais
		foreach (var entry in _questEntries.Values)
		{
			entry.button.QueueFree();
			entry.descriptionLabel.QueueFree();
		}
		_questEntries.Clear();

		var activeQuests = QuestManager.Instance.GetActiveQuests();
		foreach (var questId in activeQuests)
		{
			AddQuestButton(questId);
		}
	}

	private void AddQuestButton(string questId)
	{
		var def = QuestManager.Instance.GetQuestDefinition(questId);
		if (def == null) return;

		int stage = QuestManager.Instance.GetQuestStage(questId);
		string description = "";
		if (stage > 0 && stage <= def.Stages.Count)
			description = def.Stages[stage - 1].Description;
		else if (stage > def.Stages.Count)
			description = "Concluída";

		// Cria o botão
		var button = new Button();
		button.Text = def.Title;
		button.SizeFlagsHorizontal = Control.SizeFlags.Expand;

		// Cria o label de descrição (inicialmente oculto)
		var descLabel = new Label();
		descLabel.Text = description;
		descLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand;
		descLabel.Visible = false;
		
		// Armazena
		_questEntries[questId] = (button, descLabel);

		// Adiciona ao container
		_questListContainer.AddChild(button);
		_questListContainer.AddChild(descLabel);

		// Conecta o evento de clique do botão
		button.Pressed += () => ToggleDescription(descLabel);
	}

	private void ToggleDescription(Label label)
	{
		label.Visible = !label.Visible;
	}
}
