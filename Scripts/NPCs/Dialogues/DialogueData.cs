using Godot;
using System.Collections.Generic;

public class DialogueEntry
{
	public string Condition { get; set; }
	public List<string> Lines { get; set; }
	public List<Dictionary<string, string>> Actions { get; set; } // cada ação é um dicionário { "type": "...", "param1": "...", ... }
}

public class DialogueData
{
	public string npc_id { get; set; }
	public Godot.Collections.Array<Godot.Collections.Dictionary> dialogues { get; set; }
}
