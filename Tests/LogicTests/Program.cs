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
