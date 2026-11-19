using System;
using System.Text;

public enum TerminalKey
{
	Character, Enter, Backspace, Tab, Escape, Up, Down, Left, Right,
	Home, End, Insert, Delete, PageUp, PageDown,
	F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
}

public static class TerminalKeyboard
{
	public static byte[] Encode(
		TerminalKey key,
		string text = "",
		bool control = false,
		bool alt = false,
		bool shift = false,
		bool applicationCursor = false,
		bool newLineMode = false
	)
	{
		int modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (control ? 4 : 0);
		string sequence;
		if (key is TerminalKey.Up or TerminalKey.Down or TerminalKey.Left or TerminalKey.Right or TerminalKey.Home or TerminalKey.End)
		{
			char final = key switch
			{
				TerminalKey.Up => 'A', TerminalKey.Down => 'B', TerminalKey.Right => 'C',
				TerminalKey.Left => 'D', TerminalKey.Home => 'H', _ => 'F'
			};
			sequence = modifier != 1 ? $"\u001b[1;{modifier}{final}"
				: $"\u001b{(applicationCursor ? 'O' : '[')}{final}";
		}
		else if (key >= TerminalKey.F1 && key <= TerminalKey.F4)
		{
			char final = (char)('P' + key - TerminalKey.F1);
			sequence = modifier == 1 ? $"\u001bO{final}" : $"\u001b[1;{modifier}{final}";
		}
		else if (key is TerminalKey.Insert or TerminalKey.Delete or TerminalKey.PageUp or TerminalKey.PageDown || key >= TerminalKey.F5)
		{
			int number = key switch
			{
				TerminalKey.Insert => 2, TerminalKey.Delete => 3,
				TerminalKey.PageUp => 5, TerminalKey.PageDown => 6,
				TerminalKey.F5 => 15, TerminalKey.F6 => 17, TerminalKey.F7 => 18,
				TerminalKey.F8 => 19, TerminalKey.F9 => 20, TerminalKey.F10 => 21,
				TerminalKey.F11 => 23, _ => 24
			};
			sequence = modifier == 1 ? $"\u001b[{number}~" : $"\u001b[{number};{modifier}~";
		}
		else
		{
			sequence = key switch
			{
				TerminalKey.Enter => newLineMode ? "\r\n" : "\r",
				TerminalKey.Backspace => control ? "\u0017" : "\u007f",
				TerminalKey.Tab => shift ? "\u001b[Z" : "\t",
				TerminalKey.Escape => "\u001b",
				_ => EncodeCharacter(text, control)
			};
			if (alt && sequence.Length > 0) sequence = "\u001b" + sequence;
		}
		return Encoding.UTF8.GetBytes(sequence);
	}

	private static string EncodeCharacter(string text, bool control)
	{
		if (!control || string.IsNullOrEmpty(text)) return text;
		char character = char.ToUpperInvariant(text[0]);
		if (character >= '@' && character <= '_') return ((char)(character & 0x1f)).ToString();
		return character switch
		{
			' ' or '2' => "\0", '3' => "\u001b", '4' => "\u001c", '5' => "\u001d",
			'6' => "\u001e", '7' or '/' => "\u001f", '8' or '?' => "\u007f",
			_ => text
		};
	}

	public static byte[] Paste(string text, bool bracketed)
	{
		text = text.Replace("\r\n", "\n").Replace('\r', '\n');
		return Encoding.UTF8.GetBytes(bracketed ? "\u001b[200~" + text + "\u001b[201~" : text);
	}
}
