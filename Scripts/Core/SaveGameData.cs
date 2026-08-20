public class SaveGameData
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; }
	public Dictionary<string, int> ActiveQuests { get; set; } = new();
	public List<string> CompletedQuests { get; set; } = new();
	public Dictionary<string, int> Items { get; set; } = new();
	public int Experience { get; set; }
	public int Credits { get; set; }
	public bool OfficeWifiConnected { get; set; }
	public List<string> ClaimedQuestRewards { get; set; } = new();
}

public static class SaveGameMigration
{
	private const string InsertedQuestId = "sudo_with_less";
	private const string ExistingSuccessorId = "university_exam";

	public static bool Migrate(SaveGameData data)
	{
		if (data == null)
			return false;

		NormalizeCollections(data);

		if (data.SchemaVersion >= SaveGameData.CurrentSchemaVersion)
			return false;

		bool reachedExistingSuccessor =
			data.ActiveQuests.ContainsKey(ExistingSuccessorId) ||
			data.CompletedQuests.Contains(ExistingSuccessorId);

		if (reachedExistingSuccessor)
		{
			if (!data.CompletedQuests.Contains(InsertedQuestId))
				data.CompletedQuests.Add(InsertedQuestId);

			data.ActiveQuests.Remove(InsertedQuestId);

			if (!data.ClaimedQuestRewards.Contains(InsertedQuestId))
				data.ClaimedQuestRewards.Add(InsertedQuestId);
		}

		data.SchemaVersion = SaveGameData.CurrentSchemaVersion;
		return true;
	}

	private static void NormalizeCollections(SaveGameData data)
	{
		data.ActiveQuests ??= new Dictionary<string, int>();
		data.CompletedQuests ??= new List<string>();
		data.Items ??= new Dictionary<string, int>();
		data.ClaimedQuestRewards ??= new List<string>();
	}
}
