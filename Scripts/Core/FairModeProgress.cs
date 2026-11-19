using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class FairModeProgress
{
	private const string SaveFilePath = "user://fair_mode_scores.json";
	private const int MaximumNameLength = 18;

	public sealed class ChallengeProgress
	{
		public string Name { get; set; } = "";
		public int Attempts { get; set; }
		public int Victories { get; set; }
		public int BestScore { get; set; }
		public int TotalPoints { get; set; }
	}

	public sealed class ParticipantRecord
	{
		public string SessionId { get; set; } = "";
		public string Name { get; set; } = "";
		public string StartedAtUtc { get; set; } = "";
		public int CompletedRounds { get; set; }
		public int Victories { get; set; }
		public int TotalPoints { get; set; }
		public Dictionary<string, ChallengeProgress> Challenges { get; set; } = new();
	}

	private sealed class FairModeSaveData
	{
		public string ActiveSessionId { get; set; } = "";
		public List<ParticipantRecord> Participants { get; set; } = new();
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static FairModeSaveData _data = new();
	private static bool _loaded;
	public static string LastError { get; private set; } = "";

	public static ParticipantRecord GetActiveParticipant()
	{
		EnsureLoaded();
		if (string.IsNullOrEmpty(_data.ActiveSessionId))
			return null;

		return _data.Participants.Find(
			participant => participant.SessionId == _data.ActiveSessionId
		);
	}

	public static bool HasActiveParticipant()
	{
		return GetActiveParticipant() != null;
	}

	public static bool StartNewParticipant(string rawName, out ParticipantRecord participant)
	{
		EnsureLoaded();
		participant = null;
		LastError = "";
		string name = NormalizeName(rawName);
		if (name.Length < 2)
		{
			LastError = "Digite um nome com pelo menos 2 caracteres.";
			return false;
		}

		string previousActiveSessionId = _data.ActiveSessionId;
		participant = new ParticipantRecord
		{
			SessionId = Guid.NewGuid().ToString("N"),
			Name = name,
			StartedAtUtc = DateTime.UtcNow.ToString("O")
		};

		_data.Participants.Add(participant);
		_data.ActiveSessionId = participant.SessionId;
		if (!Save())
		{
			_data.Participants.Remove(participant);
			_data.ActiveSessionId = previousActiveSessionId;
			participant = null;
			LastError = "Não foi possível gravar a nova sessão neste computador.";
			return false;
		}
		return true;
	}

	public static void RecordResult(
		string challengeId,
		string challengeName,
		bool victory,
		int points
	)
	{
		ParticipantRecord participant = GetActiveParticipant();
		if (participant == null || string.IsNullOrWhiteSpace(challengeId))
		{
			GD.PrintErr("FairModeProgress: resultado ignorado porque não há participante ativo.");
			return;
		}

		points = Math.Max(0, points);
		participant.CompletedRounds++;
		if (victory)
			participant.Victories++;
		participant.TotalPoints = SafeAdd(participant.TotalPoints, points);

		participant.Challenges ??= new Dictionary<string, ChallengeProgress>();
		if (!participant.Challenges.TryGetValue(challengeId, out ChallengeProgress progress))
		{
			progress = new ChallengeProgress { Name = challengeName };
			participant.Challenges[challengeId] = progress;
		}

		progress.Name = challengeName;
		progress.Attempts++;
		if (victory)
			progress.Victories++;
		progress.BestScore = Math.Max(progress.BestScore, points);
		progress.TotalPoints = SafeAdd(progress.TotalPoints, points);
		Save();
	}

	public static List<ParticipantRecord> GetRanking()
	{
		EnsureLoaded();
		var ranking = new List<ParticipantRecord>(_data.Participants);
		ranking.Sort((first, second) =>
		{
			int byScore = second.TotalPoints.CompareTo(first.TotalPoints);
			if (byScore != 0)
				return byScore;
			int byVictories = second.Victories.CompareTo(first.Victories);
			if (byVictories != 0)
				return byVictories;
			return first.StartedAtUtc.CompareTo(second.StartedAtUtc);
		});
		return ranking;
	}

	public static bool ResetAll()
	{
		FairModeSaveData previousData = _data;
		_data = new FairModeSaveData();
		_loaded = true;
		if (Save())
			return true;
		_data = previousData;
		return false;
	}

	private static string NormalizeName(string rawName)
	{
		if (string.IsNullOrWhiteSpace(rawName))
			return "";

		string[] parts = rawName.Trim().Split(
			new[] { ' ', '\t', '\r', '\n' },
			StringSplitOptions.RemoveEmptyEntries
		);
		string name = string.Join(" ", parts);
		return name.Length <= MaximumNameLength
			? name
			: name.Substring(0, MaximumNameLength);
	}

	private static int SafeAdd(int current, int addition)
	{
		long result = (long)current + addition;
		return result >= int.MaxValue ? int.MaxValue : (int)result;
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
			return;

		_loaded = true;
		_data = new FairModeSaveData();
		if (!FileAccess.FileExists(SaveFilePath))
			return;

		try
		{
			using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Read);
			if (file == null)
				return;

			FairModeSaveData loaded = JsonSerializer.Deserialize<FairModeSaveData>(
				file.GetAsText(),
				JsonOptions
			);
			if (loaded == null)
				return;

			loaded.Participants ??= new List<ParticipantRecord>();
			foreach (ParticipantRecord participant in loaded.Participants)
				participant.Challenges ??= new Dictionary<string, ChallengeProgress>();
			_data = loaded;
		}
		catch (Exception exception)
		{
			GD.PrintErr($"FairModeProgress: dados locais inválidos: {exception.Message}");
			_data = new FairModeSaveData();
		}
	}

	private static bool Save()
	{
		try
		{
			string json = JsonSerializer.Serialize(_data, JsonOptions);
			using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PrintErr("FairModeProgress: não foi possível gravar os dados locais.");
				return false;
			}

			file.StoreString(json);
			return true;
		}
		catch (Exception exception)
		{
			GD.PrintErr($"FairModeProgress: erro ao salvar: {exception.Message}");
			return false;
		}
	}
}
