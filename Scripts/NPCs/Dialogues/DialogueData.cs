using Godot;
using System.Collections.Generic;

public class DialogueEntry
{
	public string Id { get; set; } = "";
	public string Condition { get; set; } = "";
	public bool DirectOnly { get; set; }
	public List<string> Lines { get; set; } = new();
	public List<Dictionary<string, string>> Actions { get; set; } = new();
}

public class DialogueData
{
	public string npc_id { get; set; }
	public Godot.Collections.Array<Godot.Collections.Dictionary> dialogues { get; set; }
}
