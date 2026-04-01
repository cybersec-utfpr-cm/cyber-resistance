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

			var stages = new List<QuestStage>();
			foreach (var stageVar in stagesArray)
			{
				var stageDict = stageVar.AsGodotDictionary();
				stages.Add(new QuestStage
				{
					StageId = stageDict["id"].AsInt32(),
					Description = stageDict["description"].AsString()
				});
			}

			_questDefinitions[id] = new QuestDefinition
			{
				Id = id,
				Title = title,
				Stages = stages,
				IsMain = isMain,
				Reward = reward
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

		_activeQuests[questId] = 1; // começa no estágio 1
		GD.Print($"QuestManager: Missão '{questId}' iniciada no estágio 1.");
		EmitSignal(SignalName.QuestStarted, questId);
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
		if (currentStage < def.Stages.Count)
		{
			_activeQuests[questId] = currentStage + 1;
			GD.Print($"QuestManager: Missão '{questId}' avançou para estágio {_activeQuests[questId]}");
			EmitSignal(SignalName.QuestAdvanced, questId, _activeQuests[questId]);

			// Se completou todos os estágios, conclui
			if (_activeQuests[questId] > def.Stages.Count)
			{
				CompleteQuest(questId);
			}
		}
		else
		{
			GD.Print($"QuestManager: Missão '{questId}' já completou todos os estágios.");
		}
	}

	// Conclui a missão (remove dos ativos e adiciona aos completados)
	public void CompleteQuest(string questId)
	{
		if (_activeQuests.ContainsKey(questId))
		{
			_activeQuests.Remove(questId);
			if (!_completedQuests.Contains(questId))
				_completedQuests.Add(questId);
			GD.Print($"QuestManager: Missão '{questId}' concluída!");
			EmitSignal(SignalName.QuestCompleted, questId);
		}
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

		// Se não estiver ativa, adiciona aos ativos
		if (!_activeQuests.ContainsKey(questId))
			_activeQuests[questId] = stage;
		else
			_activeQuests[questId] = stage;

		GD.Print($"QuestManager: Missão '{questId}' estágio definido para {stage}.");
		EmitSignal(SignalName.QuestAdvanced, questId, stage);

		// Verifica se completou todos os estágios
		var def = _questDefinitions[questId];
		if (stage > def.Stages.Count)
			CompleteQuest(questId);
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
}

// Classes auxiliares
public class QuestStage
{
	public int StageId { get; set; }
	public string Description { get; set; }
}

public class QuestDefinition
{
	public string Id { get; set; }
	public string Title { get; set; }
	public List<QuestStage> Stages { get; set; }
	public bool IsMain { get; set; }
	public string Reward { get; set; }
}
