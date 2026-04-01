using Godot;
using System.Collections.Generic;

public partial class QuestManager : Node
{
	public static QuestManager Instance { get; private set; }

	private Dictionary<string, int> _questStages = new();
	private Dictionary<string, QuestDefinition> _questDefinitions = new();

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

		// Carrega definições
		foreach (var questVar in questsArray)
		{
			var questDict = questVar.AsGodotDictionary();
			var id = questDict["id"].AsString();
			var title = questDict["title"].AsString();
			var stagesArray = questDict["stages"].AsGodotArray();

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

			_questDefinitions[id] = new QuestDefinition { Id = id, Title = title, Stages = stages };
		}

		// Inicializa estágios (todos começam em 0)
		foreach (var quest in _questDefinitions)
		{
			_questStages[quest.Key] = 0;
			GD.Print($"QuestManager: Missão '{quest.Key}' inicializada no estágio 0.");
		}
	}

	public int GetQuestStage(string questId)
	{
		if (!_questStages.ContainsKey(questId))
		{
			GD.PrintErr($"QuestManager: Missão '{questId}' não encontrada!");
			return -1;
		}
		return _questStages[questId];
	}

	public void SetQuestStage(string questId, int stage)
	{
		if (!_questStages.ContainsKey(questId))
		{
			GD.PrintErr($"QuestManager: Tentativa de definir estágio para missão inexistente '{questId}'.");
			return;
		}
		_questStages[questId] = stage;
		GD.Print($"QuestManager: Missão '{questId}' agora está no estágio {stage}");
	}

	public void AdvanceQuest(string questId)
	{
		if (_questStages.ContainsKey(questId))
		{
			_questStages[questId]++;
			GD.Print($"QuestManager: Missão '{questId}' avançou para estágio {_questStages[questId]}");
		}
	}
}

// Classes auxiliares (devem estar fora da classe QuestManager)
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
}
