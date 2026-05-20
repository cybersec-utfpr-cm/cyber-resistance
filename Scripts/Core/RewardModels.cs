using System.Collections.Generic;

public class RewardItemEntry
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public int Amount { get; set; } = 1;
}

public class RewardDefinition
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public int Xp { get; set; } = 0;
	public int Credits { get; set; } = 0;
	public List<RewardItemEntry> Items { get; set; } = new();
}
