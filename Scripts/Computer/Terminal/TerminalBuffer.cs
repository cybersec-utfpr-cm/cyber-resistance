using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

[Flags]
public enum TerminalAttributes
{
	None = 0,
	Bold = 1,
	Dim = 2,
	Italic = 4,
	Underline = 8,
	Inverse = 16,
	Hidden = 32,
	Strike = 64
}

public readonly record struct TerminalCell(
	string Text,
	int Foreground,
	int Background,
	TerminalAttributes Attributes,
	int Width = 1
);

// Screen state and VT control sequences, independent of Godot and the transport.
public sealed class TerminalBuffer
{
	private enum ParseState
	{
		Text, Escape, Csi, String, StringEscape, Charset
	}
	private static readonly TerminalCell _empty = new(" ", -1, -1, TerminalAttributes.None);
	private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
	private readonly StringBuilder _sequence = new();
	private readonly List<TerminalCell[]> _history = new();
	private readonly int _historyLimit;
	private Page _primary;
	private Page _alternate;
	private bool _alternateActive;
	private ParseState _parseState;
	private TerminalCell _style = _empty;
	private bool[] _tabs;
	private readonly bool[] _lineDrawing = new bool[2];
	private int _charsetTarget;
	private int _charset;
	private bool _oscString;
	private bool _autoWrap = true;
	private bool _originMode;
	private bool _insertMode;

	public event Action<byte[]> DataGenerated;
	public int Columns { get; private set; }
	public int Rows { get; private set; }
	public int CursorColumn => Current.Column;
	public int CursorRow => Current.Row;
	public int HistoryCount => _alternateActive ? 0 : _history.Count;
	public bool AlternateScreenActive => _alternateActive;
	public bool CursorVisible { get; private set; } = true;
	public bool ApplicationCursorKeys { get; private set; }
	public bool ApplicationKeypad { get; private set; }
	public bool BracketedPaste { get; private set; }
	public bool NewLineMode { get; private set; }
	private Page Current => _alternateActive ? _alternate : _primary;

	private sealed class Page
	{
		public TerminalCell[][] Lines;
		public int Column;
		public int Row;
		public int Top;
		public int Bottom;
		public bool WrapPending;
		public int SavedColumn;
		public int SavedRow;
		public TerminalCell SavedStyle = _empty;
		public bool SavedOrigin;

		public Page(int columns, int rows)
		{
			Lines = new TerminalCell[rows][];
			for (int row = 0; row < rows; row++)
				Lines[row] = NewLine(columns, _empty);
			Bottom = rows - 1;
		}
	}

	public TerminalBuffer(int columns = 80, int rows = 24, int historyLimit = 5000)
	{
		Columns = Math.Max(1, columns);
		Rows = Math.Max(1, rows);
		_historyLimit = Math.Max(0, historyLimit);
		_primary = new Page(Columns, Rows);
		_alternate = new Page(Columns, Rows);
		_tabs = CreateTabs(Columns);
	}

	public void Reset()
	{
		_primary = new Page(Columns, Rows);
		_alternate = new Page(Columns, Rows);
		_alternateActive = false;
		_history.Clear();
		_decoder.Reset();
		_sequence.Clear();
		_parseState = ParseState.Text;
		_tabs = CreateTabs(Columns);
		_lineDrawing[0] = _lineDrawing[1] = false;
		_charset = 0;
		ResetModes();
	}

	private void ResetModes()
	{
		_style = _empty;
		_autoWrap = true;
		_originMode = _insertMode = false;
		ApplicationCursorKeys = ApplicationKeypad = BracketedPaste = NewLineMode = false;
		CursorVisible = true;
		Current.Top = 0;
		Current.Bottom = Rows - 1;
		Current.WrapPending = false;
	}

	public void Write(byte[] data)
	{
		char[] characters = new char[Encoding.UTF8.GetMaxCharCount(data.Length)];
		int count = _decoder.GetChars(data, 0, data.Length, characters, 0, false);
		foreach (Rune character in new string(characters, 0, count).EnumerateRunes())
			Process(character);
	}

	public void Resize(int columns, int rows)
	{
		columns = Math.Max(1, columns);
		rows = Math.Max(1, rows);
		if (Columns == columns && Rows == rows)
			return;

		ResizePage(_primary, columns, rows, true);
		ResizePage(_alternate, columns, rows, false);
		bool[] tabs = CreateTabs(columns);
		Array.Copy(_tabs, tabs, Math.Min(columns, _tabs.Length));
		_tabs = tabs;
		Columns = columns;
		Rows = rows;
	}

	private void ResizePage(Page page, int columns, int rows, bool saveHistory)
	{
		int removed = Math.Max(0, page.Row - rows + 1);
		if (saveHistory)
		{
			for (int row = 0; row < removed; row++)
				AddHistory(page.Lines[row]);
		}

		var lines = new TerminalCell[rows][];
		for (int row = 0; row < rows; row++)
		{
			lines[row] = NewLine(columns, _empty);
			if (row + removed < page.Lines.Length)
				Array.Copy(page.Lines[row + removed], lines[row], Math.Min(Columns, columns));
			NormalizeLine(lines[row]);
		}
		page.Lines = lines;
		page.Column = Math.Min(page.Column, columns - 1);
		page.Row = Math.Clamp(page.Row - removed, 0, rows - 1);
		page.SavedColumn = Math.Min(page.SavedColumn, columns - 1);
		page.SavedRow = Math.Clamp(page.SavedRow - removed, 0, rows - 1);
		page.Top = 0;
		page.Bottom = rows - 1;
		page.WrapPending = false;
	}

	public TerminalCell GetCell(int column, int row, int scrollOffset = 0)
	{
		return GetAbsoluteCell(column, HistoryCount - Math.Clamp(scrollOffset, 0, HistoryCount) + row);
	}

	private TerminalCell GetAbsoluteCell(int column, int row)
	{
		if (row < 0 || column < 0)
			return _empty;
		TerminalCell[] line = row < HistoryCount
			? _history[row]
			: row - HistoryCount < Rows ? Current.Lines[row - HistoryCount] : null;
		return line != null && column < line.Length ? line[column] : _empty;
	}

	public string GetText(bool includeHistory = false)
	{
		return CopyText(0, includeHistory ? 0 : HistoryCount, Columns - 1, HistoryCount + Rows - 1);
	}

	public string CopyText(int startColumn, int startRow, int endColumn, int endRow)
	{
		if (startRow > endRow || (startRow == endRow && startColumn > endColumn))
		{
			(startRow, endRow) = (endRow, startRow);
			(startColumn, endColumn) = (endColumn, startColumn);
		}
		startRow = Math.Clamp(startRow, 0, HistoryCount + Rows - 1);
		endRow = Math.Clamp(endRow, 0, HistoryCount + Rows - 1);
		var text = new StringBuilder();
		for (int row = startRow; row <= endRow; row++)
		{
			int first = row == startRow ? Math.Clamp(startColumn, 0, Columns - 1) : 0;
			int last = row == endRow ? Math.Clamp(endColumn, 0, Columns - 1) : Columns - 1;
			var line = new StringBuilder();
			for (int column = first; column <= last; column++)
			{
				TerminalCell cell = GetAbsoluteCell(column, row);
				if (cell.Width != 0)
					line.Append(cell.Text);
			}
			text.Append(line.ToString().TrimEnd(' '));
			if (row != endRow)
				text.Append('\n');
		}
		return text.ToString();
	}

	private void Process(Rune character)
	{
		int value = character.Value;
		if (_parseState is ParseState.String or ParseState.StringEscape)
		{
			if ((_oscString && value == 7) || (_parseState == ParseState.StringEscape && value == '\\'))
				_parseState = ParseState.Text;
			else
				_parseState = value == 27 ? ParseState.StringEscape : ParseState.String;
			return;
		}

		if (value == 27)
		{
			_parseState = ParseState.Escape;
			return;
		}
		if (value is 24 or 26)
		{
			_parseState = ParseState.Text;
			return;
		}
		if (value < 32 || value == 127)
		{
			ProcessControl(value);
			return;
		}

		switch (_parseState)
		{
			case ParseState.Escape:
				ProcessEscape(value);
				break;
			case ParseState.Charset:
				if (_charsetTarget >= 0)
					_lineDrawing[_charsetTarget] = value == '0';
				_parseState = ParseState.Text;
				break;
			case ParseState.Csi:
				if (value >= 0x40 && value <= 0x7e)
				{
					ExecuteCsi((char)value);
					_parseState = ParseState.Text;
				}
				else if (value >= 0x20 && value < 0x40 && _sequence.Length < 256)
				{
					_sequence.Append((char)value);
				}
				else
				{
					_parseState = ParseState.Text;
				}
				break;
			default:
				Print(character);
				break;
		}
	}

	private void ProcessControl(int value)
	{
		switch (value)
		{
			case 8: Current.Column = Math.Max(0, Current.Column - 1); Current.WrapPending = false; break;
			case 9: MoveTab(1); break;
			case 10:
			case 11:
			case 12:
				Index();
				if (NewLineMode) Current.Column = 0;
				break;
			case 13: Current.Column = 0; Current.WrapPending = false; break;
			case 14: _charset = 1; break;
			case 15: _charset = 0; break;
		}
	}

	private void ProcessEscape(int value)
	{
		_parseState = ParseState.Text;
		switch (value)
		{
			case '[': _sequence.Clear(); _parseState = ParseState.Csi; break;
			case ']': _oscString = true; _parseState = ParseState.String; break;
			case 'P':
			case '_':
			case '^': _oscString = false; _parseState = ParseState.String; break;
			case '(':
			case ')':
			case '*':
			case '+':
			case '%':
			case '#':
				_charsetTarget = value == '(' ? 0 : value == ')' ? 1 : -1;
				_parseState = ParseState.Charset;
				break;
			case '7': SaveCursor(); break;
			case '8': RestoreCursor(); break;
			case 'D': Index(); break;
			case 'E': Current.Column = 0; Index(); break;
			case 'M':
				if (Current.Row == Current.Top) ScrollDown(1);
				else Current.Row = Math.Max(0, Current.Row - 1);
				Current.WrapPending = false;
				break;
			case 'H': _tabs[Current.Column] = true; break;
			case '=': ApplicationKeypad = true; break;
			case '>': ApplicationKeypad = false; break;
			case 'c': Reset(); break;
		}
	}

	private void ExecuteCsi(char command)
	{
		string sequence = _sequence.ToString();
		bool privateMode = sequence.StartsWith('?');
		string[] parts = sequence.TrimStart('?', '>', '<', '=').TrimEnd(' ', '!').Replace(':', ';').Split(';');
		int[] values = new int[parts.Length];
		for (int index = 0; index < parts.Length; index++)
			values[index] = int.TryParse(parts[index], out int number) ? Math.Clamp(number, 0, 65535) : 0;
		int first = values[0];
		int count = Math.Max(1, first);
		int minRow = _originMode ? Current.Top : 0;
		int maxRow = _originMode ? Current.Bottom : Rows - 1;

		switch (command)
		{
			case 'A': MoveCursor(Current.Column, Math.Max(minRow, Current.Row - count)); break;
			case 'B':
			case 'e': MoveCursor(Current.Column, Math.Min(maxRow, Current.Row + count)); break;
			case 'C':
			case 'a': MoveCursor(Current.Column + count, Current.Row); break;
			case 'D': MoveCursor(Current.Column - count, Current.Row); break;
			case 'E': MoveCursor(0, Math.Min(maxRow, Current.Row + count)); break;
			case 'F': MoveCursor(0, Math.Max(minRow, Current.Row - count)); break;
			case 'G':
			case '`': MoveCursor(count - 1, Current.Row); break;
			case 'd': MoveCursor(Current.Column, Math.Min(maxRow, minRow + count - 1)); break;
			case 'H':
			case 'f':
				MoveCursor(values.Length > 1 ? Math.Max(1, values[1]) - 1 : 0, Math.Min(maxRow, minRow + count - 1));
				break;
			case 'J': EraseDisplay(first); break;
			case 'K': EraseLine(Current.Row, first == 0 ? Current.Column : 0, first == 1 ? Current.Column + 1 : Columns); break;
			case 'X': EraseLine(Current.Row, Current.Column, Math.Min(Columns, Current.Column + count)); break;
			case '@': InsertCharacters(count); break;
			case 'P': DeleteCharacters(count); break;
			case 'L': InsertLines(count, false); break;
			case 'M': InsertLines(count, true); break;
			case 'S': ScrollUp(count); break;
			case 'T': ScrollDown(count); break;
			case 'I': MoveTab(count); break;
			case 'Z': MoveTab(-count); break;
			case 'g':
				if (first == 0) _tabs[Current.Column] = false;
				else if (first == 3) Array.Fill(_tabs, false);
				break;
			case 'r':
				if (privateMode) break;
				int top = count - 1;
				int bottom = values.Length > 1 && values[1] != 0 ? values[1] - 1 : Rows - 1;
				if (top >= 0 && bottom < Rows && top < bottom)
				{
					Current.Top = top;
					Current.Bottom = bottom;
					MoveCursor(0, _originMode ? top : 0);
				}
				break;
			case 's': SaveCursor(); break;
			case 'u': RestoreCursor(); break;
			case 'm': SetGraphics(values, sequence.Contains(':')); break;
			case 'h':
			case 'l': SetModes(values, privateMode, command == 'h'); break;
			case 'n':
				if (first == 5) Reply("\u001b[0n");
				else if (first == 6)
					Reply($"\u001b[{(privateMode ? "?" : "")}{Current.Row - minRow + 1};{Current.Column + 1}R");
				break;
			case 'c': Reply(sequence.StartsWith('>') ? "\u001b[>0;1;0c" : "\u001b[?1;2c"); break;
			case 't': if (first == 18) Reply($"\u001b[8;{Rows};{Columns}t"); break;
			case 'p': if (sequence.EndsWith('!')) ResetModes(); break;
		}
	}

	private void SetModes(int[] modes, bool privateMode, bool enabled)
	{
		foreach (int mode in modes)
		{
			if (!privateMode)
			{
				if (mode == 4) _insertMode = enabled;
				if (mode == 20) NewLineMode = enabled;
				continue;
			}
			switch (mode)
			{
				case 1: ApplicationCursorKeys = enabled; break;
				case 6: _originMode = enabled; MoveCursor(0, enabled ? Current.Top : 0); break;
				case 7: _autoWrap = enabled; Current.WrapPending = false; break;
				case 25: CursorVisible = enabled; break;
				case 47:
				case 1047:
				case 1049:
					if (enabled == _alternateActive) break;
					if (enabled)
					{
						if (mode == 1049) SaveCursor();
						if (mode != 47) _alternate = new Page(Columns, Rows);
						_alternateActive = true;
					}
					else
					{
						_alternateActive = false;
						if (mode == 1049) RestoreCursor();
					}
					break;
				case 1048: if (enabled) SaveCursor(); else RestoreCursor(); break;
				case 2004: BracketedPaste = enabled; break;
			}
		}
	}

	private void SetGraphics(int[] values, bool colonSeparated)
	{
		for (int index = 0; index < values.Length; index++)
		{
			int value = values[index];
			TerminalAttributes attributes = _style.Attributes;
			switch (value)
			{
				case 0: _style = _empty; continue;
				case 1: attributes |= TerminalAttributes.Bold; break;
				case 2: attributes |= TerminalAttributes.Dim; break;
				case 3: attributes |= TerminalAttributes.Italic; break;
				case 4: attributes |= TerminalAttributes.Underline; break;
				case 7: attributes |= TerminalAttributes.Inverse; break;
				case 8: attributes |= TerminalAttributes.Hidden; break;
				case 9: attributes |= TerminalAttributes.Strike; break;
				case 22: attributes &= ~(TerminalAttributes.Bold | TerminalAttributes.Dim); break;
				case 23: attributes &= ~TerminalAttributes.Italic; break;
				case 24: attributes &= ~TerminalAttributes.Underline; break;
				case 27: attributes &= ~TerminalAttributes.Inverse; break;
				case 28: attributes &= ~TerminalAttributes.Hidden; break;
				case 29: attributes &= ~TerminalAttributes.Strike; break;
				case 39: _style = _style with { Foreground = -1 }; break;
				case 49: _style = _style with { Background = -1 }; break;
				case >= 30 and <= 37: _style = _style with { Foreground = value - 30 }; break;
				case >= 40 and <= 47: _style = _style with { Background = value - 40 }; break;
				case >= 90 and <= 97: _style = _style with { Foreground = value - 90 + 8 }; break;
				case >= 100 and <= 107: _style = _style with { Background = value - 100 + 8 }; break;
				case 38:
				case 48:
					int color = -1;
					if (index + 2 < values.Length && values[index + 1] == 5)
					{
						color = Math.Clamp(values[index + 2], 0, 255);
						index += 2;
					}
					else if (index + 4 < values.Length && values[index + 1] == 2)
					{
						int offset = colonSeparated && index + 5 < values.Length ? 3 : 2;
						color = 0x1000000 |
							(Math.Clamp(values[index + offset], 0, 255) << 16) |
							(Math.Clamp(values[index + offset + 1], 0, 255) << 8) |
							Math.Clamp(values[index + offset + 2], 0, 255);
						index += offset + 2;
					}
					if (color >= 0)
						_style = value == 38 ? _style with { Foreground = color } : _style with { Background = color };
					break;
			}
			_style = _style with { Attributes = attributes };
		}
	}

	private void Print(Rune character)
	{
		UnicodeCategory category = Rune.GetUnicodeCategory(character);
		if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
		{
			int previous = Current.WrapPending ? Current.Column : Current.Column - 1;
			if (previous >= 0)
			{
				TerminalCell[] line = Current.Lines[Current.Row];
				if (line[previous].Width == 0 && previous > 0) previous--;
				line[previous] = line[previous] with { Text = line[previous].Text + character.ToString() };
			}
			return;
		}

		int value = character.Value;
		int width = value >= 0x1100 && (
			value <= 0x115f || value is 0x2329 or 0x232a ||
			(value >= 0x2e80 && value <= 0xa4cf && value != 0x303f) ||
			(value >= 0xac00 && value <= 0xd7a3) || (value >= 0xf900 && value <= 0xfaff) ||
			(value >= 0xfe10 && value <= 0xfe6f) || (value >= 0xff01 && value <= 0xff60) ||
			(value >= 0xffe0 && value <= 0xffe6) || (value >= 0x1f300 && value <= 0x1faff) ||
			(value >= 0x20000 && value <= 0x3fffd)
		) ? 2 : 1;
		width = Math.Min(width, Columns);
		if (Current.WrapPending || (width == 2 && Current.Column == Columns - 1 && _autoWrap))
		{
			Current.Column = 0;
			Index();
		}
		width = Math.Min(width, Columns - Current.Column);
		if (_insertMode) InsertCharacters(width);
		TerminalCell[] cells = Current.Lines[Current.Row];
		ClearGlyphAt(cells, Current.Column);
		if (width == 2) ClearGlyphAt(cells, Current.Column + 1);
		string text = _lineDrawing[_charset] ? LineDrawing(character) : character.ToString();
		cells[Current.Column] = _style with { Text = text, Width = width };
		if (width == 2) cells[Current.Column + 1] = _style with { Text = "", Width = 0 };
		int next = Current.Column + width;
		Current.Column = Math.Min(next, Columns - 1);
		Current.WrapPending = next >= Columns && _autoWrap;
	}

	private static string LineDrawing(Rune character)
	{
		return character.Value switch
		{
			'j' => "┘", 'k' => "┐", 'l' => "┌", 'm' => "└", 'n' => "┼",
			'q' => "─", 't' => "├", 'u' => "┤", 'v' => "┴", 'w' => "┬", 'x' => "│",
			'`' => "◆", 'a' => "▒", 'f' => "°", 'g' => "±", '~' => "·",
			_ => character.ToString()
		};
	}

	private void MoveCursor(int column, int row)
	{
		Current.Column = Math.Clamp(column, 0, Columns - 1);
		Current.Row = Math.Clamp(row, 0, Rows - 1);
		Current.WrapPending = false;
	}

	private void SaveCursor()
	{
		Current.SavedColumn = Current.Column;
		Current.SavedRow = Current.Row;
		Current.SavedStyle = _style;
		Current.SavedOrigin = _originMode;
	}

	private void RestoreCursor()
	{
		MoveCursor(Current.SavedColumn, Current.SavedRow);
		_style = Current.SavedStyle;
		_originMode = Current.SavedOrigin;
	}

	private void Index()
	{
		if (Current.Row == Current.Bottom) ScrollUp(1);
		else Current.Row = Math.Min(Rows - 1, Current.Row + 1);
		Current.WrapPending = false;
	}

	private void ScrollUp(int count)
	{
		count = Math.Min(count, Current.Bottom - Current.Top + 1);
		for (int index = 0; index < count; index++)
		{
			if (!_alternateActive && Current.Top == 0 && Current.Bottom == Rows - 1)
				AddHistory(Current.Lines[0]);
			Array.Copy(Current.Lines, Current.Top + 1, Current.Lines, Current.Top, Current.Bottom - Current.Top);
			Current.Lines[Current.Bottom] = NewLine(Columns, _style);
		}
	}

	private void ScrollDown(int count)
	{
		count = Math.Min(count, Current.Bottom - Current.Top + 1);
		for (int index = 0; index < count; index++)
		{
			Array.Copy(Current.Lines, Current.Top, Current.Lines, Current.Top + 1, Current.Bottom - Current.Top);
			Current.Lines[Current.Top] = NewLine(Columns, _style);
		}
	}

	private void AddHistory(TerminalCell[] line)
	{
		if (_historyLimit == 0) return;
		_history.Add(line);
		if (_history.Count > _historyLimit) _history.RemoveAt(0);
	}

	private void EraseDisplay(int mode)
	{
		if (mode == 3)
		{
			_history.Clear();
			return;
		}
		for (int row = 0; row < Rows; row++)
		{
			if (mode == 2 || (mode == 0 && row > Current.Row) || (mode == 1 && row < Current.Row))
				EraseLine(row, 0, Columns);
			else if (row == Current.Row)
				EraseLine(row, mode == 0 ? Current.Column : 0, mode == 1 ? Current.Column + 1 : Columns);
		}
		Current.WrapPending = false;
	}

	private void EraseLine(int row, int start, int end)
	{
		for (int column = start; column < end; column++)
			Current.Lines[row][column] = _style with { Text = " ", Width = 1 };
		NormalizeLine(Current.Lines[row]);
		Current.WrapPending = false;
	}

	private void InsertCharacters(int count)
	{
		count = Math.Min(count, Columns - Current.Column);
		TerminalCell[] line = Current.Lines[Current.Row];
		Array.Copy(line, Current.Column, line, Current.Column + count, Columns - Current.Column - count);
		EraseLine(Current.Row, Current.Column, Current.Column + count);
	}

	private void DeleteCharacters(int count)
	{
		count = Math.Min(count, Columns - Current.Column);
		TerminalCell[] line = Current.Lines[Current.Row];
		Array.Copy(line, Current.Column + count, line, Current.Column, Columns - Current.Column - count);
		EraseLine(Current.Row, Columns - count, Columns);
	}

	private void InsertLines(int count, bool delete)
	{
		if (Current.Row < Current.Top || Current.Row > Current.Bottom) return;
		count = Math.Min(count, Current.Bottom - Current.Row + 1);
		int length = Current.Bottom - Current.Row + 1 - count;
		if (delete)
			Array.Copy(Current.Lines, Current.Row + count, Current.Lines, Current.Row, length);
		else
			Array.Copy(Current.Lines, Current.Row, Current.Lines, Current.Row + count, length);
		int first = delete ? Current.Bottom - count + 1 : Current.Row;
		for (int row = first; row < first + count; row++)
			Current.Lines[row] = NewLine(Columns, _style);
		Current.WrapPending = false;
	}

	private void MoveTab(int count)
	{
		int direction = Math.Sign(count);
		for (int index = 0; index < Math.Min(Math.Abs(count), Columns); index++)
		{
			do
			{
				Current.Column = Math.Clamp(Current.Column + direction, 0, Columns - 1);
			}
			while (Current.Column > 0 && Current.Column < Columns - 1 && !_tabs[Current.Column]);
		}
		Current.WrapPending = false;
	}

	private static bool[] CreateTabs(int columns)
	{
		var tabs = new bool[columns];
		for (int column = 8; column < columns; column += 8) tabs[column] = true;
		return tabs;
	}

	private static TerminalCell[] NewLine(int columns, TerminalCell style)
	{
		var cells = new TerminalCell[columns];
		Array.Fill(cells, style with { Text = " ", Width = 1 });
		return cells;
	}

	private static void ClearGlyphAt(TerminalCell[] line, int column)
	{
		if (line[column].Width == 0 && column > 0) line[column - 1] = _empty;
		if (line[column].Width == 2 && column + 1 < line.Length) line[column + 1] = _empty;
	}

	private static void NormalizeLine(TerminalCell[] line)
	{
		for (int column = 0; column < line.Length; column++)
		{
			if (line[column].Width == 2 && (column + 1 == line.Length || line[column + 1].Width != 0))
				line[column] = _empty;
			else if (line[column].Width == 0 && (column == 0 || line[column - 1].Width != 2))
				line[column] = _empty;
		}
	}

	private void Reply(string text) => DataGenerated?.Invoke(Encoding.ASCII.GetBytes(text));
}
