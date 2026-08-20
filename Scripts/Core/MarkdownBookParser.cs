using System.Text;
using System.Text.RegularExpressions;

public static class MarkdownBookParser
{
	private static readonly Regex UnorderedListPattern = new(
		@"^(?<indent>\s*)[-+*]\s+(?<content>.+)$"
	);
	private static readonly Regex OrderedListPattern = new(
		@"^(?<indent>\s*)(?<number>\d+)\.\s+(?<content>.+)$"
	);
	private static readonly Regex HorizontalRulePattern = new(
		@"^\s*((-{3,})|(\*{3,})|(_{3,}))\s*$"
	);
	private static readonly Regex LinkPattern = new(
		@"\[(?<label>[^\]]+)\]\([^\)]+\)"
	);
	private static readonly Regex InlineCodePattern = new(@"`(?<text>[^`]+)`");
	private static readonly Regex StrongPattern = new(
		@"(\*\*(?<asterisk>.+?)\*\*)|__(?<underscore>.+?)__"
	);
	private static readonly Regex EmphasisPattern = new(
		@"(\*(?<asterisk>[^*]+?)\*)|_(?<underscore>[^_]+?)_"
	);

	public static MarkdownBookDocument Parse(string markdown)
	{
		if (string.IsNullOrWhiteSpace(markdown))
			throw new InvalidDataException("o conteúdo está vazio.");

		string[] lines = markdown
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n')
			.Split('\n');
		string title = "";
		string currentChapterTitle = null;
		var currentChapterLines = new List<string>();
		var chapters = new List<MarkdownBookChapter>();
		bool insideFence = false;

		foreach (string line in lines)
		{
			if (IsFence(line))
			{
				if (currentChapterTitle != null)
					currentChapterLines.Add(line);

				insideFence = !insideFence;
				continue;
			}

			if (!insideFence && TryReadHeading(line, out int level, out string heading))
			{
				if (level == 1 && string.IsNullOrWhiteSpace(title))
				{
					title = ToDisplayText(heading);
					continue;
				}

				if (level == 2)
				{
					AddChapter(chapters, currentChapterTitle, currentChapterLines);
					currentChapterTitle = ToDisplayText(heading);
					currentChapterLines.Clear();
					continue;
				}
			}

			if (currentChapterTitle != null)
				currentChapterLines.Add(line);
		}

		AddChapter(chapters, currentChapterTitle, currentChapterLines);

		if (string.IsNullOrWhiteSpace(title))
			throw new InvalidDataException("não foi encontrado um título '#'.");

		if (chapters.Count == 0)
			throw new InvalidDataException("não foi encontrado nenhum capítulo '##'.");

		return new MarkdownBookDocument(title, chapters);
	}

	private static void AddChapter(
		List<MarkdownBookChapter> chapters,
		string title,
		List<string> lines
	)
	{
		if (title == null)
			return;

		if (string.IsNullOrWhiteSpace(title))
			throw new InvalidDataException("foi encontrado um capítulo sem título.");

		chapters.Add(
			new MarkdownBookChapter(
				$"chapter_{chapters.Count + 1}",
				title,
				ConvertBlocks(lines)
			)
		);
	}

	private static string ConvertBlocks(IReadOnlyList<string> lines)
	{
		var output = new StringBuilder();
		var paragraph = new List<string>();
		bool insideFence = false;

		foreach (string line in lines)
		{
			if (IsFence(line))
			{
				FlushParagraph(output, paragraph);
				output.AppendLine(insideFence ? "[/code]" : "[code]");
				insideFence = !insideFence;
				continue;
			}

			if (insideFence)
			{
				output.AppendLine(EscapeBbCode(line));
				continue;
			}

			if (string.IsNullOrWhiteSpace(line))
			{
				FlushParagraph(output, paragraph);
				AppendBlankLine(output);
				continue;
			}

			if (TryReadHeading(line, out int level, out string heading) && level >= 3)
			{
				FlushParagraph(output, paragraph);
				int fontSize = level == 3 ? 22 : 19;
				output.Append("[font_size=");
				output.Append(fontSize);
				output.Append("][b]");
				output.Append(RenderInline(heading));
				output.AppendLine("[/b][/font_size]");
				continue;
			}

			if (HorizontalRulePattern.IsMatch(line))
			{
				FlushParagraph(output, paragraph);
				output.AppendLine("────────────────────────────────");
				continue;
			}

			Match unorderedMatch = UnorderedListPattern.Match(line);
			if (unorderedMatch.Success)
			{
				FlushParagraph(output, paragraph);
				output.Append(GetListIndent(unorderedMatch.Groups["indent"].Value));
				output.Append("• ");
				output.AppendLine(RenderInline(unorderedMatch.Groups["content"].Value));
				continue;
			}

			Match orderedMatch = OrderedListPattern.Match(line);
			if (orderedMatch.Success)
			{
				FlushParagraph(output, paragraph);
				output.Append(GetListIndent(orderedMatch.Groups["indent"].Value));
				output.Append(orderedMatch.Groups["number"].Value);
				output.Append(". ");
				output.AppendLine(RenderInline(orderedMatch.Groups["content"].Value));
				continue;
			}

			string trimmedLine = line.TrimStart();
			if (trimmedLine.StartsWith(">", StringComparison.Ordinal))
			{
				FlushParagraph(output, paragraph);
				string quote = trimmedLine[1..].TrimStart();
				output.Append("│ [i]");
				output.Append(RenderInline(quote));
				output.AppendLine("[/i]");
				continue;
			}

			paragraph.Add(line.Trim());
		}

		FlushParagraph(output, paragraph);
		if (insideFence)
			output.AppendLine("[/code]");

		return output.ToString().Trim();
	}

	private static void FlushParagraph(
		StringBuilder output,
		List<string> paragraph
	)
	{
		if (paragraph.Count == 0)
			return;

		output.AppendLine(RenderInline(string.Join(" ", paragraph)));
		paragraph.Clear();
	}

	private static void AppendBlankLine(StringBuilder output)
	{
		if (output.Length == 0 || output[^1] != '\n')
			output.AppendLine();

		if (output.Length < 2 || output[^2] != '\n')
			output.AppendLine();
	}

	private static string GetListIndent(string indentation)
	{
		return new string('\t', indentation.Length / 2);
	}

	private static string RenderInline(string markdown)
	{
		var output = new StringBuilder();
		int position = 0;

		while (position < markdown.Length)
		{
			if (markdown[position] == '`')
			{
				int closing = markdown.IndexOf('`', position + 1);
				if (closing >= 0)
				{
					output.Append("[code]");
					output.Append(EscapeBbCode(markdown[(position + 1)..closing]));
					output.Append("[/code]");
					position = closing + 1;
					continue;
				}
			}

			if (markdown[position] == '[')
			{
				int labelEnd = markdown.IndexOf("](", position, StringComparison.Ordinal);
				if (labelEnd >= 0)
				{
					int targetEnd = markdown.IndexOf(')', labelEnd + 2);
					if (targetEnd >= 0)
					{
						string label = markdown[(position + 1)..labelEnd];
						output.Append("[u]");
						output.Append(RenderInline(label));
						output.Append("[/u]");
						position = targetEnd + 1;
						continue;
					}
				}
			}

			int nextSpecial = FindNextInlineSpecial(markdown, position + 1);
			int segmentEnd = nextSpecial < 0 ? markdown.Length : nextSpecial;
			output.Append(RenderEmphasis(markdown[position..segmentEnd]));
			position = segmentEnd;
		}

		return output.ToString();
	}

	private static int FindNextInlineSpecial(string markdown, int start)
	{
		int code = markdown.IndexOf('`', start);
		int link = markdown.IndexOf('[', start);

		if (code < 0)
			return link;

		if (link < 0)
			return code;

		return Math.Min(code, link);
	}

	private static string RenderEmphasis(string text)
	{
		var output = new StringBuilder();
		int position = 0;

		while (position < text.Length)
		{
			if (text[position] == '\\' && position + 1 < text.Length)
			{
				AppendEscapedCharacter(output, text[position + 1]);
				position += 2;
				continue;
			}

			string delimiter = GetEmphasisDelimiter(text, position);
			if (delimiter != null)
			{
				int closing = text.IndexOf(
					delimiter,
					position + delimiter.Length,
					StringComparison.Ordinal
				);

				if (closing > position + delimiter.Length)
				{
					bool strong = delimiter.Length == 2;
					output.Append(strong ? "[b]" : "[i]");
					output.Append(
						RenderEmphasis(
							text[(position + delimiter.Length)..closing]
						)
					);
					output.Append(strong ? "[/b]" : "[/i]");
					position = closing + delimiter.Length;
					continue;
				}
			}

			AppendEscapedCharacter(output, text[position]);
			position++;
		}

		return output.ToString();
	}

	private static string GetEmphasisDelimiter(string text, int position)
	{
		char current = text[position];
		if (current != '*' && current != '_')
			return null;

		if (position + 1 < text.Length && text[position + 1] == current)
			return new string(current, 2);

		return current.ToString();
	}

	private static string EscapeBbCode(string text)
	{
		var output = new StringBuilder(text.Length);
		foreach (char character in text)
			AppendEscapedCharacter(output, character);

		return output.ToString();
	}

	private static void AppendEscapedCharacter(StringBuilder output, char character)
	{
		switch (character)
		{
			case '[':
				output.Append("[lb]");
				break;
			case ']':
				output.Append("[rb]");
				break;
			default:
				output.Append(character);
				break;
		}
	}

	private static bool TryReadHeading(
		string line,
		out int level,
		out string heading
	)
	{
		level = 0;
		while (level < line.Length && line[level] == '#')
			level++;

		if (
			level == 0 ||
			level > 6 ||
			level >= line.Length ||
			line[level] != ' '
		)
		{
			heading = "";
			return false;
		}

		heading = line[(level + 1)..].Trim();
		return true;
	}

	private static bool IsFence(string line)
	{
		return line.TrimStart().StartsWith("```", StringComparison.Ordinal);
	}

	private static string ToDisplayText(string markdown)
	{
		string text = LinkPattern.Replace(markdown, "${label}");
		text = InlineCodePattern.Replace(text, "${text}");
		text = StrongPattern.Replace(
			text,
			match => match.Groups["asterisk"].Success
				? match.Groups["asterisk"].Value
				: match.Groups["underscore"].Value
		);
		text = EmphasisPattern.Replace(
			text,
			match => match.Groups["asterisk"].Success
				? match.Groups["asterisk"].Value
				: match.Groups["underscore"].Value
		);
		return text.Trim();
	}
}

public sealed class MarkdownBookDocument
{
	public MarkdownBookDocument(
		string title,
		IReadOnlyList<MarkdownBookChapter> chapters
	)
	{
		Title = title;
		Chapters = chapters;
	}

	public string Title { get; }
	public IReadOnlyList<MarkdownBookChapter> Chapters { get; }
}

public sealed class MarkdownBookChapter
{
	public MarkdownBookChapter(string id, string title, string content)
	{
		Id = id;
		Title = title;
		Content = content;
	}

	public string Id { get; }
	public string Title { get; }
	public string Content { get; }
}
