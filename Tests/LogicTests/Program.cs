using System.Text.Json;

public static class Program
{
	private static int _passed;
	private static int _failed;

	public static int Main()
	{
		Run("extrai título e capítulos", ExtractsTitleAndChapters);
		Run("converte Markdown suportado", ConvertsSupportedMarkdown);
		Run("escapa BBCode não confiável", EscapesUntrustedBbCode);
		Run("rejeita documentos inválidos", RejectsInvalidDocuments);
		Run("processa o material real do Scenario1", ParsesScenarioMaterial);
		Run("valida dados e cadeia da Sudo with Less", ValidatesSudoWithLessData);
		Run("migra progresso legado já avançado", MigratesAdvancedLegacyProgress);
		Run("preserva progresso legado anterior à missão", PreservesEarlierLegacyProgress);

		Console.WriteLine($"\nResultado: {_passed} passou; {_failed} falhou.");
		return _failed == 0 ? 0 : 1;
	}

	private static void Run(string name, Action test)
	{
		try
		{
			test();
			_passed++;
			Console.WriteLine($"PASSOU: {name}");
		}
		catch (Exception exception)
		{
			_failed++;
			Console.Error.WriteLine($"FALHOU: {name}");
			Console.Error.WriteLine(exception.Message);
		}
	}

	private static void ExtractsTitleAndChapters()
	{
		const string markdown = """
			# Livro de Teste

			Texto antes dos capítulos não deve virar capítulo.

			## Primeiro capítulo

			Conteúdo inicial.

			### Subtítulo interno

			Conteúdo do subtítulo.

			## Segundo capítulo com `código`

			Conteúdo final.
			""";

		MarkdownBookDocument document = MarkdownBookParser.Parse(markdown);

		Equal("Livro de Teste", document.Title);
		Equal(2, document.Chapters.Count);
		Equal("chapter_1", document.Chapters[0].Id);
		Equal("Primeiro capítulo", document.Chapters[0].Title);
		Equal("Segundo capítulo com código", document.Chapters[1].Title);
		Contains(
			"[font_size=22][b]Subtítulo interno[/b][/font_size]",
			document.Chapters[0].Content
		);
		DoesNotContain(
			"Texto antes dos capítulos",
			document.Chapters[0].Content
		);
	}

	private static void ConvertsSupportedMarkdown()
	{
		const string markdown = """
			# Formatação
			## Elementos
			Um texto com **negrito**, *itálico* e `sudo -l`.

			- item sem ordem
			  - item aninhado
			1. primeiro item
			2. segundo item

			> **Aviso:** ambiente controlado.

			[Referência](#destino)

			---

			```bash
			echo "teste"
			```
			""";

		string content = MarkdownBookParser.Parse(markdown).Chapters[0].Content;

		Contains("[b]negrito[/b]", content);
		Contains("[i]itálico[/i]", content);
		Contains("[code]sudo -l[/code]", content);
		Contains("• item sem ordem", content);
		Contains("\t• item aninhado", content);
		Contains("1. primeiro item", content);
		Contains("│ [i][b]Aviso:[/b] ambiente controlado.[/i]", content);
		Contains("[u]Referência[/u]", content);
		Contains("────────────────", content);
		Contains("[code]\necho \"teste\"\n[/code]", content);
	}

	private static void EscapesUntrustedBbCode()
	{
		const string markdown = """
			# Segurança
			## Escape
			Texto [b]não confiável[/b] e **confiável**.

			`[url=ataque]clique[/url]`

			```
			[font_size=99]conteúdo[/font_size]
			```
			""";

		string content = MarkdownBookParser.Parse(markdown).Chapters[0].Content;

		Contains("[lb]b[rb]não confiável[lb]/b[rb]", content);
		Contains("[b]confiável[/b]", content);
		Contains("[code][lb]url=ataque[rb]clique[lb]/url[rb][/code]", content);
		Contains("[lb]font_size=99[rb]conteúdo[lb]/font_size[rb]", content);
		DoesNotContain("[b]não confiável[/b]", content);
		DoesNotContain("[font_size=99]", content);
	}

	private static void RejectsInvalidDocuments()
	{
		Throws<InvalidDataException>(
			() => MarkdownBookParser.Parse("## Capítulo\nConteúdo"),
			"título"
		);
		Throws<InvalidDataException>(
			() => MarkdownBookParser.Parse("# Livro sem capítulos"),
			"capítulo"
		);
		Throws<InvalidDataException>(
			() => MarkdownBookParser.Parse("  \n"),
			"vazio"
		);
	}

	private static void ParsesScenarioMaterial()
	{
		string materialPath = FindRepositoryFile(
			"Materials/Scenario1/material-didatico.md"
		);
		string markdown = File.ReadAllText(materialPath);
		MarkdownBookDocument document = MarkdownBookParser.Parse(markdown);

		Equal("Material Didático ― Cenário 1: Sudo with Less", document.Title);
		Equal(19, document.Chapters.Count);
		Equal("Sumário", document.Chapters[0].Title);
		Equal("4. O que é sudo", document.Chapters[4].Title);
		True(
			document.Chapters.Any(
				chapter => chapter.Content.Contains(
					"[code]\nwhoami\n[/code]",
					StringComparison.Ordinal
				)
			),
			"O bloco de código com 'whoami' não foi convertido."
		);
		True(
			document.Chapters.All(chapter => !string.IsNullOrWhiteSpace(chapter.Content)),
			"O material real produziu um capítulo vazio."
		);
	}

	private static void ValidatesSudoWithLessData()
	{
		using JsonDocument questsDocument = ReadJsonFile("Data/quests.json");
		JsonElement quests = questsDocument.RootElement.GetProperty("quests");
		JsonElement wifiQuest = FindById(quests, "wifi_hacking");
		JsonElement sudoQuest = FindById(quests, "sudo_with_less");
		JsonElement universityQuest = FindById(quests, "university_exam");

		Equal("sudo_with_less", wifiQuest.GetProperty("next_quest_id").GetString());
		Equal("university_exam", sudoQuest.GetProperty("next_quest_id").GetString());
		True(
			!universityQuest.TryGetProperty("next_quest_id", out _),
			"A última missão da cadeia não deve declarar sucessora."
		);
		Equal("npc_acceptance", sudoQuest.GetProperty("start_mode").GetString());
		Equal("hubner", sudoQuest.GetProperty("interaction_npc_id").GetString());
		Equal(
			"sudo_with_less_material",
			sudoQuest.GetProperty("material_book_id").GetString()
		);
		Equal(
			"sudo_with_less_lab",
			sudoQuest.GetProperty("infrastructure_id").GetString()
		);
		Equal(1, sudoQuest.GetProperty("stages").GetArrayLength());
		Equal(1, sudoQuest.GetProperty("optional_objectives").GetArrayLength());

		using JsonDocument rewardsDocument = ReadJsonFile("Data/rewards.json");
		FindById(
			rewardsDocument.RootElement.GetProperty("rewards"),
			sudoQuest.GetProperty("reward_id").GetString()
		);

		using JsonDocument booksDocument = ReadJsonFile("Data/books.json");
		JsonElement materialBook = FindById(
			booksDocument.RootElement.GetProperty("books"),
			"sudo_with_less_material"
		);
		Equal("sudo_with_less", materialBook.GetProperty("unlock_quest_id").GetString());

		string infrastructurePath = FindRepositoryFile(
			"Data/missionInfrastructure.json"
		);
		string infrastructureJson = File.ReadAllText(infrastructurePath);
		using JsonDocument infrastructureDocument =
			JsonDocument.Parse(infrastructureJson);
		JsonElement infrastructures =
			infrastructureDocument.RootElement.GetProperty("infrastructures");
		JsonElement player = FindById(infrastructures, "player_machine");
		JsonElement lab = FindById(infrastructures, "sudo_with_less_lab");

		Equal("127.0.0.1", PlayerHostIp(player));
		Equal("127.0.0.1", PlayerHostIp(lab));
		Equal("cyber_resistance", lab.GetProperty("network").GetProperty("name").GetString());
		Equal("bob", lab.GetProperty("credentials").GetProperty("username").GetString());
		Equal("password", lab.GetProperty("credentials").GetProperty("password").GetString());
		Equal(22, lab.GetProperty("readiness").GetProperty("port").GetInt32());
		Equal("/root/flag.txt", lab.GetProperty("flag_target").GetProperty("path").GetString());
		DoesNotContain("Flag da missão:", infrastructureJson);
	}

	private static void MigratesAdvancedLegacyProgress()
	{
		const string legacyJson = """
			{
			  "ActiveQuests": { "university_exam": 2 },
			  "CompletedQuests": ["tutorial", "wifi_hacking"],
			  "ClaimedQuestRewards": ["tutorial", "wifi_hacking"]
			}
			""";
		SaveGameData data = JsonSerializer.Deserialize<SaveGameData>(legacyJson);

		Equal(0, data.SchemaVersion);
		True(SaveGameMigration.Migrate(data), "O save legado não foi migrado.");
		Equal(SaveGameData.CurrentSchemaVersion, data.SchemaVersion);
		Equal(2, data.ActiveQuests["university_exam"]);
		True(
			data.CompletedQuests.Contains("sudo_with_less"),
			"A missão inserida não foi retroativamente concluída."
		);
		True(
			data.ClaimedQuestRewards.Contains("sudo_with_less"),
			"A recompensa da missão pulada ficou indevidamente disponível."
		);
		True(
			!SaveGameMigration.Migrate(data),
			"A migração não é idempotente."
		);
	}

	private static void PreservesEarlierLegacyProgress()
	{
		var data = new SaveGameData
		{
			CompletedQuests = new List<string> { "tutorial", "wifi_hacking" }
		};

		True(SaveGameMigration.Migrate(data), "O schema legado não foi atualizado.");
		True(
			!data.CompletedQuests.Contains("sudo_with_less"),
			"Um save que ainda não chegou à prova pulou a nova missão."
		);
		True(
			!data.ClaimedQuestRewards.Contains("sudo_with_less"),
			"A recompensa foi marcada sem a missão ter sido pulada."
		);
	}

	private static JsonDocument ReadJsonFile(string relativePath)
	{
		return JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(relativePath)));
	}

	private static JsonElement FindById(JsonElement array, string id)
	{
		foreach (JsonElement element in array.EnumerateArray())
		{
			if (element.GetProperty("id").GetString() == id)
				return element;
		}

		throw new InvalidOperationException($"ID não encontrado nos dados: {id}");
	}

	private static string PlayerHostIp(JsonElement infrastructure)
	{
		return infrastructure
			.GetProperty("host_bindings")[0]
			.GetProperty("host_ip")
			.GetString();
	}

	private static string FindRepositoryFile(string relativePath)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory != null)
		{
			string candidate = Path.Combine(
				directory.FullName,
				relativePath.Replace('/', Path.DirectorySeparatorChar)
			);
			if (File.Exists(candidate))
				return candidate;

			directory = directory.Parent;
		}

		throw new FileNotFoundException(
			$"Arquivo de teste não encontrado: {relativePath}"
		);
	}

	private static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException(
				$"Esperado: '{expected}'. Obtido: '{actual}'."
			);
		}
	}

	private static void Contains(string expected, string actual)
	{
		if (!actual.Contains(expected, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Trecho esperado não encontrado: {expected}\nConteúdo:\n{actual}"
			);
		}
	}

	private static void DoesNotContain(string unexpected, string actual)
	{
		if (actual.Contains(unexpected, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Trecho inseguro encontrado: {unexpected}\nConteúdo:\n{actual}"
			);
		}
	}

	private static void True(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	private static void Throws<TException>(Action action, string messageFragment)
		where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException exception)
		{
			if (
				exception.Message.Contains(
					messageFragment,
					StringComparison.OrdinalIgnoreCase
				)
			)
			{
				return;
			}

			throw new InvalidOperationException(
				$"A exceção não menciona '{messageFragment}': {exception.Message}"
			);
		}

		throw new InvalidOperationException(
			$"Era esperada a exceção {typeof(TException).Name}."
		);
	}
}
