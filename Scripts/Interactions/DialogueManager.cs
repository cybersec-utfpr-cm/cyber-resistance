using Godot;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;

public partial class DialogueManager : Node
{
	public static DialogueManager Instance { get; private set; }

	// Dicionário: NPC ID -> Lista de diálogos (cada diálogo é uma lista de falas)
	private Dictionary<string, List<DialogueEntry>> _npcDialogues = new();

	// Referência à caixa de diálogo (será instanciada)
	private DialogBox _dialogBox;

	// Flag para evitar múltiplos diálogos simultâneos
	private bool _isDialogueActive = false;
	
	private List<Dictionary<string, string>> _pendingActions;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		// Carrega todos os JSONs da pasta de diálogos
		LoadAllDialogues();

		// Instancia a caixa de diálogo e a adiciona à árvore
		var dialogScene = GD.Load<PackedScene>("res://Scenes/Interactions/dialog_box.tscn");
		_dialogBox = dialogScene.Instantiate<DialogBox>();
		
		// Usar CallDeferred para adicionar com segurança (espera a árvore da cena ser criada)
		GetTree().Root.CallDeferred(Node.MethodName.AddChild, _dialogBox);
		  
		_dialogBox.Hide();
	}

	private void LoadAllDialogues()
	{
		string path = "res://Scripts/NPCs/Dialogues/";
		using var dir = DirAccess.Open(path);
		if (dir == null)
		{
			GD.PrintErr("DialogueManager: Pasta de diálogos não encontrada: " + path);
			return;
		}

		var files = dir.GetFiles();
		foreach (var fileName in files)
		{
			if (!fileName.EndsWith(".json"))
				continue;

			string fullPath = path + fileName;
			LoadDialogueFile(fullPath);
		}
	}

	private void LoadDialogueFile(string filePath)
	{
		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"DialogueManager: Não foi possível abrir o arquivo: {filePath}");
			return;
		}

		string content = file.GetAsText();
		var json = new Json();
		var parseResult = json.Parse(content);
		if (parseResult != Error.Ok)
		{
			GD.PrintErr($"DialogueManager: Erro ao parsear JSON {filePath}: {parseResult}");
			return;
		}

		var data = json.Data.AsGodotDictionary();

		if (!data.ContainsKey("npc_id") || !data.ContainsKey("dialogues"))
		{
			GD.PrintErr($"DialogueManager: Arquivo {filePath} não contém 'npc_id' ou 'dialogues'.");
			return;
		}

		string npcId = data["npc_id"].AsString();
		var dialoguesArray = data["dialogues"].AsGodotArray();

		List<DialogueEntry> dialoguesList = new();

		foreach (var dialogueVar in dialoguesArray)
		{
			var dialogueDict = dialogueVar.AsGodotDictionary();

			// Extrai a condição (opcional)
			string condition = "";
			if (dialogueDict.ContainsKey("condition"))
			{
				condition = dialogueDict["condition"].AsString();
			}

			// Extrai as linhas (obrigatório)
			if (!dialogueDict.ContainsKey("lines"))
			{
				GD.PrintErr($"DialogueManager: Diálogo sem 'lines' no arquivo {filePath}. Ignorando.");
				continue;
			}
			
			// Extrai as ações
			List<Dictionary<string, string>> actions = new();
			if (dialogueDict.ContainsKey("on_end"))
			{
				var actionsArray = dialogueDict["on_end"].AsGodotArray();
				foreach (var actionVar in actionsArray)
				{
					var actionDict = actionVar.AsGodotDictionary();
					var action = new Dictionary<string, string>();
					foreach (var key in actionDict.Keys)
					{
						action[key.ToString()] = actionDict[key].ToString();
					}
					actions.Add(action);
				}
			}

			var linesArray = dialogueDict["lines"].AsGodotArray();
			List<string> lines = new();
			foreach (var lineVar in linesArray)
			{
				lines.Add(lineVar.AsString());
			}

			dialoguesList.Add(new DialogueEntry
			{
				Condition = condition,
				Lines = lines,
				Actions = actions
			});
		}

		if (_npcDialogues.ContainsKey(npcId))
		{
			GD.Print($"DialogueManager: Aviso - NPC ID '{npcId}' já existe. Substituindo.");
			_npcDialogues[npcId] = dialoguesList;
		}
		else
		{
			_npcDialogues.Add(npcId, dialoguesList);
		}

		GD.Print($"DialogueManager: Diálogos carregados para NPC '{npcId}' do arquivo {filePath}");
	}

	// Método chamado por um NPC para iniciar o diálogo
	public void StartDialogue(string npcId)
	{
		if (_isDialogueActive)
		{
			GD.Print("DialogueManager: Já existe um diálogo ativo.");
			return;
		}

		if (!_npcDialogues.ContainsKey(npcId))
		{
			GD.PrintErr($"DialogueManager: Nenhum diálogo encontrado para NPC ID '{npcId}'.");
			return;
		}

		var dialogues = _npcDialogues[npcId];
		DialogueEntry selectedDialogue = null;

		foreach (var entry in dialogues)
		{
			bool eval = ConditionEvaluator.Evaluate(entry.Condition);
			GD.Print($"Avaliando condição: {entry.Condition} -> {eval}");
			if (eval)
			{
				selectedDialogue = entry;
				GD.Print($"Diálogo selecionado com condição: {entry.Condition}");
				break;
			}
		}

		if (selectedDialogue == null)
		{
			GD.PrintErr($"DialogueManager: NPC '{npcId}' não tem diálogo com condição verdadeira.");
			return;
		}

		if (selectedDialogue.Lines == null || selectedDialogue.Lines.Count == 0)
		{
			GD.PrintErr($"DialogueManager: Diálogo selecionado para NPC '{npcId}' não tem linhas.");
			return;
		}

		_pendingActions = selectedDialogue.Actions; // pode ser null
		_isDialogueActive = true;
		_dialogBox.StartDialogue(selectedDialogue.Lines);
		_dialogBox.DialogFinished += OnDialogFinished;
	}

	private void OnDialogFinished()
	{
		_isDialogueActive = false;
		_dialogBox.DialogFinished -= OnDialogFinished;

		GD.Print($"DialogueManager: Diálogo finalizado. Ações pendentes: {(_pendingActions != null ? _pendingActions.Count : 0)}");
		if (_pendingActions != null)
		{
			ActionProcessor.ExecuteActions(_pendingActions);
			_pendingActions = null;
		}
	}

	// Chamado pelo NPC quando o jogador pressiona E durante o diálogo
	public void AdvanceDialogue()
	{
		if (_isDialogueActive)
			_dialogBox.Advance();
	}

	public bool IsDialogueActive()
	{
		return _isDialogueActive;
	}
}

public static class ConditionEvaluator
{
	public static bool Evaluate(string condition)
	{
		if (string.IsNullOrWhiteSpace(condition))
			return true; // Sem condição = sempre verdadeiro

		// Lista de operadores suportados
		string[] operators = { "==", "!=", ">=", "<=", ">", "<" };
		string op = null;
		foreach (var candidate in operators)
		{
			if (condition.Contains(candidate))
			{
				op = candidate;
				break;
			}
		}

		if (op == null)
		{
			GD.PrintErr($"ConditionEvaluator: Operador não encontrado na condição: {condition}");
			return false;
		}

		var parts = condition.Split(new[] { op }, StringSplitOptions.None);
		if (parts.Length != 2)
		{
			GD.PrintErr($"ConditionEvaluator: Formato inválido: {condition}");
			return false;
		}

		string left = parts[0].Trim();
		string right = parts[1].Trim();

		// left deve ser algo como "quest_phishing_stage"
		if (!left.StartsWith("quest_") || !left.EndsWith("_stage"))
		{
			GD.PrintErr($"ConditionEvaluator: left deve ser 'quest_NOME_stage', mas é: {left}");
			return false;
		}

		string questName = left.Substring(6, left.Length - 12); // remove "quest_" e "_stage"
		if (!int.TryParse(right, out int value))
		{
			GD.PrintErr($"ConditionEvaluator: valor deve ser inteiro, mas é: {right}");
			return false;
		}

		int currentStage = QuestManager.Instance.GetQuestStage(questName);

		return op switch
		{
			"==" => currentStage == value,
			"!=" => currentStage != value,
			">"  => currentStage > value,
			"<"  => currentStage < value,
			">=" => currentStage >= value,
			"<=" => currentStage <= value,
			_ => false
		};
	}
}

public static class ActionProcessor
{
	public static void ExecuteActions(List<Dictionary<string, string>> actions)
	{
		if (actions == null) return;
		foreach (var action in actions)
		{
			if (!action.ContainsKey("type")) continue;
			string type = action["type"];
			switch (type)
			{
				case "set_quest_stage":
					if (action.ContainsKey("quest") && action.ContainsKey("stage"))
					{
						string quest = action["quest"];
						if (int.TryParse(action["stage"], out int stage))
							QuestManager.Instance.SetQuestStage(quest, stage);
					}
					break;
				case "start_quest":
					if (action.ContainsKey("quest"))
					{
						QuestManager.Instance.StartQuest(action["quest"]);
					}
					break;
				case "give_item":
					// Implementar depois
					break;
				default:
					GD.PrintErr($"Ação desconhecida: {type}");
					break;
			}
		}
	}
}
