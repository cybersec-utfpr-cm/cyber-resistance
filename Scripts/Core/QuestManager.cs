using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class QuestManager : Node
{
	public static QuestManager Instance { get; private set; }

	// Definições carregadas do JSON
	private Dictionary<string, QuestDefinition> _questDefinitions = new();
	
	// Estado atual das missões (apenas as ativas)
	private Dictionary<string, int> _activeQuests = new();
	private List<string> _completedQuests = new();

	// Sinais para UI
	[Signal] public delegate void QuestStartedEventHandler(string questId);
	[Signal] public delegate void QuestAdvancedEventHandler(string questId, int newStage);
	[Signal] public delegate void QuestCompletedEventHandler(string questId);

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		LoadQuests();
	}

	private void LoadQuests()
	{
		string path = "res://Data/quests.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("QuestManager: Arquivo não encontrado: " + path);
			return;
		}

		string content = file.GetAsText();
		var json = new Json();
		var result = json.Parse(content);
		if (result != Error.Ok)
		{
			GD.PrintErr("QuestManager: Erro ao parsear JSON.");
			return;
		}

		var data = json.Data.AsGodotDictionary();
		var questsArray = data["quests"].AsGodotArray();

		foreach (var questVar in questsArray)
		{
			var questDict = questVar.AsGodotDictionary();
			var id = questDict["id"].AsString();
			var title = questDict["title"].AsString();
			var stagesArray = questDict["stages"].AsGodotArray();
			bool isMain = questDict.ContainsKey("is_main") ? questDict["is_main"].AsBool() : false;
			string reward = questDict.ContainsKey("reward") ? questDict["reward"].AsString() : "";
			string rewardId = questDict.ContainsKey("reward_id") ? questDict["reward_id"].AsString() : "";
			string nextQuestId = questDict.ContainsKey("next_quest_id") ? questDict["next_quest_id"].AsString() : "";
			var stages = new List<QuestStage>();
			foreach (var stageVar in stagesArray)
			{
				var stageDict = stageVar.AsGodotDictionary();
				stages.Add(new QuestStage
				{
					StageId = stageDict["id"].AsInt32(),
					Description = stageDict["description"].AsString(),
					Location = stageDict.ContainsKey("location")
						? stageDict["location"].AsString()
						: "",
					Hint = stageDict.ContainsKey("hint")
						? stageDict["hint"].AsString()
						: ""
				});
			}

			_questDefinitions[id] = new QuestDefinition
			{
				Id = id,
				Title = title,
				Stages = stages,
				IsMain = isMain,
				Reward = reward,
				RewardId = rewardId,
				NextQuestId = nextQuestId
			};
		}

		GD.Print($"QuestManager: Carregadas {_questDefinitions.Count} definições de missões.");
	}

	// Inicia uma missão (se não estiver ativa ou concluída)
	public void StartQuest(string questId)
	{
		if (!_questDefinitions.ContainsKey(questId))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' não definida.");
			return;
		}
		if (_activeQuests.ContainsKey(questId) || _completedQuests.Contains(questId))
		{
			GD.Print($"QuestManager: Missão '{questId}' já está ativa ou concluída.");
			return;
		}

		string prerequisiteQuestId = GetPrerequisiteQuestId(questId);
		if (
			!string.IsNullOrWhiteSpace(prerequisiteQuestId) &&
			!_completedQuests.Contains(prerequisiteQuestId)
		)
		{
			GD.PrintErr(
				$"QuestManager: Missão '{questId}' exige a conclusão de " +
				$"'{prerequisiteQuestId}'."
			);
			return;
		}

		_activeQuests[questId] = 1; // começa no estágio 1
		GD.Print($"QuestManager: Missão '{questId}' iniciada no estágio 1.");
		EmitSignal(SignalName.QuestStarted, questId);
		SaveManager.Instance?.SaveGame();
	}

	// Avança para o próximo estágio da missão
	public void AdvanceQuest(string questId)
	{
		if (!_activeQuests.ContainsKey(questId))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' não está ativa.");
			return;
		}

		int currentStage = _activeQuests[questId];
		var def = _questDefinitions[questId];
		if (currentStage >= def.Stages.Count)
		{
			CompleteQuest(questId);
		}
		else
		{
			SetQuestStage(questId, currentStage + 1);
		}
	}

	// Conclui a missão (remove dos ativos e adiciona aos completados)
	public void CompleteQuest(string questId)
	{
		if (!_activeQuests.TryGetValue(questId, out int currentStage))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' não está ativa.");
			return;
		}

		var definition = _questDefinitions[questId];
		if (currentStage < definition.Stages.Count)
		{
			GD.PrintErr(
				$"QuestManager: Missão '{questId}' ainda está no estágio " +
				$"{currentStage}/{definition.Stages.Count}."
			);
			return;
		}

		_activeQuests.Remove(questId);
		if (!_completedQuests.Contains(questId))
			_completedQuests.Add(questId);

		GD.Print($"QuestManager: Missão '{questId}' concluída!");
		EmitSignal(SignalName.QuestCompleted, questId);

		if (!string.IsNullOrWhiteSpace(definition.NextQuestId))
			StartQuest(definition.NextQuestId);

		SaveManager.Instance?.SaveGame();
	}

	// Define o estágio de uma missão (para uso em ações, sem avanço automático)
	public void SetQuestStage(string questId, int stage)
	{
		if (!_questDefinitions.ContainsKey(questId))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' não definida.");
			return;
		}
		if (_completedQuests.Contains(questId))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' já está concluída, não pode alterar estágio.");
			return;
		}

		if (!_activeQuests.TryGetValue(questId, out int currentStage))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' não está ativa.");
			return;
		}

		if (stage == currentStage)
			return;

		var def = _questDefinitions[questId];
		if (stage != currentStage + 1)
		{
			GD.PrintErr(
				$"QuestManager: avanço inválido de '{questId}': " +
				$"estágio {currentStage} para {stage}."
			);
			return;
		}

		if (stage > def.Stages.Count)
		{
			CompleteQuest(questId);
			return;
		}

		_activeQuests[questId] = stage;
		GD.Print($"QuestManager: Missão '{questId}' estágio definido para {stage}.");
		EmitSignal(SignalName.QuestAdvanced, questId, stage);
		SaveManager.Instance?.SaveGame();
	}

	// Consulta o estágio atual de uma missão (retorna -1 se não ativa)
	public int GetQuestStage(string questId)
	{
		if (_activeQuests.ContainsKey(questId))
			return _activeQuests[questId];
		if (_completedQuests.Contains(questId))
			return _questDefinitions[questId].Stages.Count + 1; // estágio final +1
		return -1;
	}

	// Verifica se a missão está ativa
	public bool IsQuestActive(string questId)
	{
		return _activeQuests.ContainsKey(questId);
	}

	// Verifica se a missão foi concluída
	public bool IsQuestCompleted(string questId)
	{
		return _completedQuests.Contains(questId);
	}

	// Retorna a definição de uma missão
	public QuestDefinition GetQuestDefinition(string questId)
	{
		return _questDefinitions.ContainsKey(questId) ? _questDefinitions[questId] : null;
	}

	// Retorna a lista de IDs das missões ativas
	public List<string> GetActiveQuests()
	{
		return _activeQuests.Keys.ToList();
	}
	
	public List<string> GetCompletedQuests()
	{
		return _completedQuests.ToList();
	}

	public Dictionary<string, int> GetActiveQuestStages()
	{
		return new Dictionary<string, int>(_activeQuests);
	}

	public void RestoreProgress(
		Dictionary<string, int> activeQuests,
		IEnumerable<string> completedQuests
	)
	{
		_activeQuests.Clear();
		_completedQuests.Clear();

		if (completedQuests != null)
		{
			foreach (var questId in completedQuests)
			{
				if (_questDefinitions.ContainsKey(questId))
					_completedQuests.Add(questId);
			}
		}

		RestoreCompletedPrerequisites();

		if (activeQuests != null)
		{
			foreach (var quest in activeQuests)
			{
				string prerequisiteQuestId = GetPrerequisiteQuestId(quest.Key);

				if (
					_questDefinitions.ContainsKey(quest.Key) &&
					!_completedQuests.Contains(quest.Key) &&
					(
						string.IsNullOrWhiteSpace(prerequisiteQuestId) ||
						_completedQuests.Contains(prerequisiteQuestId)
					)
				)
				{
					int maximumStage = _questDefinitions[quest.Key].Stages.Count;

					if (quest.Value > maximumStage)
						_completedQuests.Add(quest.Key);
					else
						_activeQuests[quest.Key] = System.Math.Clamp(
							quest.Value,
							1,
							maximumStage
						);
				}
			}
		}

		RestoreCompletedPrerequisites();
		RestoreMissingFollowUpQuests();
	}

	private string GetPrerequisiteQuestId(string questId)
	{
		return _questDefinitions.Values
			.FirstOrDefault(definition => definition.NextQuestId == questId)
			?.Id ?? "";
	}

	private void RestoreCompletedPrerequisites()
	{
		bool progressChanged;

		do
		{
			progressChanged = false;

			foreach (string completedQuestId in _completedQuests.ToList())
			{
				string prerequisiteQuestId =
					GetPrerequisiteQuestId(completedQuestId);

				if (
					!string.IsNullOrWhiteSpace(prerequisiteQuestId) &&
					!_completedQuests.Contains(prerequisiteQuestId)
				)
				{
					_completedQuests.Add(prerequisiteQuestId);
					progressChanged = true;
				}
			}
		}
		while (progressChanged);
	}

	private void RestoreMissingFollowUpQuests()
	{
		foreach (string completedQuestId in _completedQuests.ToList())
		{
			var definition = _questDefinitions[completedQuestId];
			string nextQuestId = definition.NextQuestId;

			if (
				!string.IsNullOrWhiteSpace(nextQuestId) &&
				_questDefinitions.ContainsKey(nextQuestId) &&
				!_completedQuests.Contains(nextQuestId) &&
				!_activeQuests.ContainsKey(nextQuestId)
			)
			{
				_activeQuests[nextQuestId] = 1;
				GD.Print(
					$"QuestManager: progresso restaurado com " +
					$"'{nextQuestId}' no estágio 1."
				);
			}
		}
	}
}// Classes auxiliares
public class QuestStage
{
	public int StageId { get; set; }
	public string Description { get; set; }
	public string Location { get; set; }
	public string Hint { get; set; }
}

public class QuestDefinition
{
	public string Id { get; set; }
	public string Title { get; set; }
	public List<QuestStage> Stages { get; set; }
	public bool IsMain { get; set; }
	public string Reward { get; set; }
	public string RewardId { get; set; }
	public string NextQuestId { get; set; }
}
