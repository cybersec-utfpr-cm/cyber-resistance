using System.Text;

public static class TerminalBufferTests
{
	private static void Write(TerminalBuffer buffer, string text) => buffer.Write(Encoding.UTF8.GetBytes(text));
	private static string Row(TerminalBuffer buffer, int row) => buffer.CopyText(0, buffer.HistoryCount + row, buffer.Columns - 1, buffer.HistoryCount + row);

	public static void PreservesFragmentedScreenState()
	{
		var buffer = new TerminalBuffer(20, 4);
		string reply = "";
		buffer.DataGenerated += data => reply += Encoding.ASCII.GetString(data);
		foreach (byte value in Encoding.UTF8.GetBytes("\u001b[2J\u001b[Hsenha: \u001b[?25l\u001b[2;3Hação\u001b[6n\u001b]0;título\u0007"))
			buffer.Write(new[] { value });

		Equal("senha:", Row(buffer, 0));
		Equal("  ação", Row(buffer, 1));
		Equal(6, buffer.CursorColumn);
		Equal(1, buffer.CursorRow);
		Equal(false, buffer.CursorVisible);
		Equal("\u001b[2;7R", reply);
	}

	public static void OverwritesAndEditsExistingLines()
	{
		var buffer = new TerminalBuffer(12, 3);
		Write(buffer, "progresso99%\rfeito\u001b[K");
		Equal("feito", Row(buffer, 0));
		Write(buffer, "\u001b[2J\u001b[HABCDE\u001b[1;3H\u001b[2@xy");
		Equal("ABxyCDE", Row(buffer, 0));
		Write(buffer, "\u001b[1;3H\u001b[2P");
		Equal("ABCDE", Row(buffer, 0));
		Write(buffer, "\u001b[1;2H\u001b[2X");
		Equal("A  DE", Row(buffer, 0));
		Write(buffer, "\u001b[2;1Hsegunda\u001b[3;1Hterceira\u001b[2;4H\u001b[0J");
		Equal("seg", Row(buffer, 1));
		Equal("", Row(buffer, 2));
	}

	public static void WrapsAndKeepsBoundedHistory()
	{
		var buffer = new TerminalBuffer(4, 2, 2);
		Write(buffer, "abcdefgh");
		Equal(0, buffer.HistoryCount);
		Write(buffer, "i");
		Equal(1, buffer.HistoryCount);
		Equal("abcd", buffer.CopyText(0, 0, 3, 0));
		Equal("efgh", Row(buffer, 0));
		Equal("i", Row(buffer, 1));
		Write(buffer, "\r\nnext\r\nlast");
		Equal(2, buffer.HistoryCount);
		Equal("next", Row(buffer, 0));
		Equal("last", Row(buffer, 1));
		Write(buffer, "\u001b[3J");
		Equal(0, buffer.HistoryCount);
		Equal("last", Row(buffer, 1));
	}

	public static void RestoresShellAfterAlternateScreen()
	{
		var buffer = new TerminalBuffer(12, 4);
		Write(buffer, "shell$ \u001b[?1049h\u001b[?1h\u001b[?2004h\u001b[31mpager");
		Equal(true, buffer.AlternateScreenActive);
		Equal(true, buffer.ApplicationCursorKeys);
		Equal(true, buffer.BracketedPaste);
		Equal("pager", Row(buffer, 0));
		Write(buffer, "\u001b[?1049l\u001b[?1l\u001b[?2004l");
		Equal(false, buffer.AlternateScreenActive);
		Equal(false, buffer.ApplicationCursorKeys);
		Equal(false, buffer.BracketedPaste);
		Equal("shell$", Row(buffer, 0));
		Equal(7, buffer.CursorColumn);
		Write(buffer, "x");
		Equal(-1, buffer.GetCell(7, 0).Foreground);
	}

	public static void LimitsScrollingToTheProgramRegion()
	{
		var buffer = new TerminalBuffer(4, 4);
		Write(buffer, "1111\r\n2222\r\n3333\r\n4444\u001b[2;3r\u001b[3;1H\n");
		Equal("1111", Row(buffer, 0));
		Equal("3333", Row(buffer, 1));
		Equal("", Row(buffer, 2));
		Equal("4444", Row(buffer, 3));
		Equal(0, buffer.HistoryCount);
		Write(buffer, "\u001b[2;1H\u001b[L");
		Equal("", Row(buffer, 1));
		Equal("3333", Row(buffer, 2));
		Write(buffer, "\u001b[M");
		Equal("3333", Row(buffer, 1));
		Write(buffer, "\u001bM");
		Equal("", Row(buffer, 1));
		Equal("3333", Row(buffer, 2));
	}

	public static void KeepsColorsAndCharacterAttributes()
	{
		var buffer = new TerminalBuffer(20, 3);
		Write(buffer, "\u001b[31;44;1;4mA\u001b[38;5;196;48;2;1;2;3mB\u001b[0mC");
		Equal(1, buffer.GetCell(0, 0).Foreground);
		Equal(4, buffer.GetCell(0, 0).Background);
		Equal(TerminalAttributes.Bold | TerminalAttributes.Underline, buffer.GetCell(0, 0).Attributes);
		Equal(196, buffer.GetCell(1, 0).Foreground);
		Equal(0x1010203, buffer.GetCell(1, 0).Background);
		Equal(-1, buffer.GetCell(2, 0).Foreground);
		Equal(TerminalAttributes.None, buffer.GetCell(2, 0).Attributes);
		Write(buffer, "\u001b(0lqk\u001b(B");
		Equal("ABC┌─┐", Row(buffer, 0));
	}

	public static void KeepsWideAndCombiningCharactersInCells()
	{
		var buffer = new TerminalBuffer(8, 3);
		Write(buffer, "a\u0301界x");
		Equal("a\u0301", buffer.GetCell(0, 0).Text);
		Equal(2, buffer.GetCell(1, 0).Width);
		Equal(0, buffer.GetCell(2, 0).Width);
		Equal(4, buffer.CursorColumn);
		Write(buffer, "\rAB");
		Equal("AB x", Row(buffer, 0));
		Equal(1, buffer.GetCell(2, 0).Width);
	}

	public static void ResizesWithoutLosingTheCursorLine()
	{
		var buffer = new TerminalBuffer(8, 3);
		Write(buffer, "line1\r\nline2\r\nline3");
		buffer.Resize(5, 2);
		Equal("line2", Row(buffer, 0));
		Equal("line3", Row(buffer, 1));
		Equal(1, buffer.HistoryCount);
		Equal(4, buffer.CursorColumn);
		Equal(1, buffer.CursorRow);
		buffer.Resize(10, 4);
		Equal("line3", Row(buffer, 1));
		Equal("", Row(buffer, 3));
		Write(buffer, "\u001bc");
		Equal(0, buffer.HistoryCount);
		Equal(0, buffer.CursorColumn);
		Equal("", Row(buffer, 0));
	}

	public static void EncodesInteractiveKeysAndPaste()
	{
		Equal("w", Text(TerminalKeyboard.Encode(TerminalKey.Character, "w")));
		Equal("\r", Text(TerminalKeyboard.Encode(TerminalKey.Enter)));
		Equal("\u001b", Text(TerminalKeyboard.Encode(TerminalKey.Escape)));
		Equal("\u007f", Text(TerminalKeyboard.Encode(TerminalKey.Backspace)));
		Equal("\u0003", Text(TerminalKeyboard.Encode(TerminalKey.Character, "c", control: true)));
		Equal("\u0004", Text(TerminalKeyboard.Encode(TerminalKey.Character, "d", control: true)));
		Equal("\t", Text(TerminalKeyboard.Encode(TerminalKey.Tab)));
		Equal("\u001b[Z", Text(TerminalKeyboard.Encode(TerminalKey.Tab, shift: true)));
		Equal("\u001b[A", Text(TerminalKeyboard.Encode(TerminalKey.Up)));
		Equal("\u001bOA", Text(TerminalKeyboard.Encode(TerminalKey.Up, applicationCursor: true)));
		Equal("\u001b[1;5A", Text(TerminalKeyboard.Encode(TerminalKey.Up, control: true)));
		Equal("\u001b[6~", Text(TerminalKeyboard.Encode(TerminalKey.PageDown)));
		Equal("\u001bOP", Text(TerminalKeyboard.Encode(TerminalKey.F1)));
		Equal("\u001b[24~", Text(TerminalKeyboard.Encode(TerminalKey.F12)));
		Equal("ação\ntexto", Text(TerminalKeyboard.Paste("ação\r\ntexto", false)));
		Equal("\u001b[200~ação\ntexto\u001b[201~", Text(TerminalKeyboard.Paste("ação\r\ntexto", true)));
	}

	private static string Text(byte[] data) => Encoding.UTF8.GetString(data);
	private static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
			throw new InvalidOperationException($"Esperado: '{expected}'; recebido: '{actual}'.");
	}
}
