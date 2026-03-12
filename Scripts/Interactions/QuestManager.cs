using Godot;
using System.Collections.Generic;

public partial class QuestManager : Node
{
	public static QuestManager Instance { get; private set; }

	private Dictionary<string, int> _questStages = new();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		// Inicializa algumas missões para teste
		SetQuestStage("phishing", 0);
	}

	public int GetQuestStage(string questName)
	{
		return _questStages.ContainsKey(questName) ? _questStages[questName] : -1;
	}

	public void SetQuestStage(string questName, int stage)
	{
		_questStages[questName] = stage;
		GD.Print($"QuestManager: Missão '{questName}' agora está no estágio {stage}");
	}
}
