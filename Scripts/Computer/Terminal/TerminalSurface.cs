using Godot;
using System;

public partial class TerminalSurface : Control
{
	[Signal]
	public delegate void DataSentEventHandler(byte[] data);
	[Signal]
	public delegate void TerminalSizeChangedEventHandler(Vector2I size);

	private readonly TerminalBuffer _buffer = new();
	private readonly Font[] _fonts = new Font[4];
	private StyleBox _normalStyle;
	private StyleBox _focusStyle;
	private Color _foreground;
	private Color _background;
	private Color _caret;
	private Vector2 _padding;
	private Vector2 _cellSize;
	private int _fontSize;
	private float _ascent;
	private int _scrollOffset;
	private bool _selecting;
	private bool _hasSelection;
	private Vector2I _selectionStart;
	private Vector2I _selectionEnd;
	private double _blinkTime;
	private bool _cursorOn = true;

	public bool InputEnabled { get; private set; }
	public int Columns => _buffer.Columns;
	public int Rows => _buffer.Rows;
	public Vector2I CursorPosition => new(_buffer.CursorColumn, _buffer.CursorRow);

	public override void _Ready()
	{
		ClipContents = true;
		FocusMode = FocusModeEnum.All;
		MouseDefaultCursorShape = CursorShape.Ibeam;
		for (int index = 0; index < _fonts.Length; index++)
		{
			_fonts[index] = new SystemFont
			{
				FontNames = new[] { "DejaVu Sans Mono", "Liberation Mono", "Consolas", "Menlo", "monospace" },
				FontWeight = (index & 1) != 0 ? 700 : 400,
				FontItalic = (index & 2) != 0
			};
		}
		_buffer.DataGenerated += SendData;
		Resized += UpdateSize;
		ThemeChanged += UpdateTheme;
		FocusEntered += ResetBlink;
		FocusExited += QueueRedraw;
		UpdateTheme();
	}

	private void UpdateTheme()
	{
		_normalStyle = GetThemeStylebox("normal", "TextEdit");
		_focusStyle = GetThemeStylebox("focus", "TextEdit");
		_foreground = GetThemeColor("font_color", "TextEdit");
		_background = _normalStyle is StyleBoxFlat flat ? flat.BgColor : new Color(0.02f, 0.03f, 0.05f);
		_caret = GetThemeColor("caret_color", "TextEdit");
		_fontSize = GetThemeFontSize("font_size", "TextEdit");
		_padding = new Vector2(_normalStyle.GetContentMargin(Side.Left), _normalStyle.GetContentMargin(Side.Top));
		_cellSize = new Vector2(Mathf.Ceil(_fonts[0].GetCharSize('M', _fontSize).X), Mathf.Ceil(_fonts[0].GetHeight(_fontSize)));
		_ascent = _fonts[0].GetAscent(_fontSize);
		UpdateSize();
	}

	private void UpdateSize()
	{
		if (_normalStyle == null || _cellSize.X <= 0 || _cellSize.Y <= 0) return;
		Vector2 area = Size - _normalStyle.GetMinimumSize();
		int columns = (int)(area.X / _cellSize.X);
		int rows = (int)(area.Y / _cellSize.Y);
		if (columns < 1 || rows < 1 || (columns == Columns && rows == Rows)) return;
		_buffer.Resize(columns, rows);
		_scrollOffset = Math.Min(_scrollOffset, _buffer.HistoryCount);
		_hasSelection = false;
		EmitSignal(SignalName.TerminalSizeChanged, new Vector2I(columns, rows));
		QueueRedraw();
	}

	public void SetInputEnabled(bool enabled)
	{
		InputEnabled = enabled;
		FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
		MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		if (!enabled) ReleaseFocus();
		QueueRedraw();
	}

	public void ResetTerminal()
	{
		_buffer.Reset();
		_scrollOffset = 0;
		_hasSelection = _selecting = false;
		ResetBlink();
	}

	public void WriteData(byte[] data)
	{
		int previousHistory = _buffer.HistoryCount;
		_buffer.Write(data);
		if (_scrollOffset > 0)
			_scrollOffset = Math.Clamp(_scrollOffset + _buffer.HistoryCount - previousHistory, 0, _buffer.HistoryCount);
		QueueRedraw();
	}

	public string GetText() => _buffer.GetText();

	public string CopySelection()
	{
		return _hasSelection
			? _buffer.CopyText(_selectionStart.X, _selectionStart.Y, _selectionEnd.X, _selectionEnd.Y)
			: string.Empty;
	}

	public override void _Input(InputEvent @event)
	{
		if (!InputEnabled || !IsVisibleInTree() || !HasFocus() || @event is not InputEventKey key) return;

		// Consume keyboard events before Godot's ui_up/ui_down navigation (also mapped to WASD).
		GetViewport().SetInputAsHandled();
		if (!key.Pressed) return;
		if ((key.CtrlPressed && key.ShiftPressed && key.Keycode == Key.C) || (key.CtrlPressed && key.Keycode == Key.Insert))
		{
			if (_hasSelection) DisplayServer.ClipboardSet(CopySelection());
			return;
		}
		if ((key.CtrlPressed && key.ShiftPressed && key.Keycode == Key.V) || (key.ShiftPressed && key.Keycode == Key.Insert))
		{
			SendUserData(TerminalKeyboard.Paste(DisplayServer.ClipboardGet(), _buffer.BracketedPaste));
			return;
		}
		if (key.ShiftPressed && key.Keycode is Key.Pageup or Key.Pagedown)
		{
			ScrollHistory(key.Keycode == Key.Pageup ? Rows : -Rows);
			return;
		}

		TerminalKey terminalKey = key.Keycode switch
		{
			Key.Enter or Key.KpEnter => TerminalKey.Enter, Key.Backspace => TerminalKey.Backspace,
			Key.Tab or Key.Backtab => TerminalKey.Tab, Key.Escape => TerminalKey.Escape,
			Key.Up => TerminalKey.Up, Key.Down => TerminalKey.Down, Key.Left => TerminalKey.Left, Key.Right => TerminalKey.Right,
			Key.Home => TerminalKey.Home, Key.End => TerminalKey.End,
			Key.Insert => TerminalKey.Insert, Key.Delete => TerminalKey.Delete,
			Key.Pageup => TerminalKey.PageUp, Key.Pagedown => TerminalKey.PageDown,
			>= Key.F1 and <= Key.F12 => TerminalKey.F1 + (int)(key.Keycode - Key.F1),
			_ => TerminalKey.Character
		};
		string text = key.Unicode > 0 ? char.ConvertFromUtf32((int)key.Unicode) : string.Empty;
		if (text.Length == 0 && key.CtrlPressed && key.Keycode >= Key.Space && key.Keycode <= Key.Asciitilde)
			text = ((char)key.Keycode).ToString();
		bool altGr = key.CtrlPressed && key.AltPressed && text.Length > 0 && key.Keycode != (Key)char.ToUpperInvariant(text[0]);
		if (_buffer.ApplicationKeypad && TryKeypad(key.Keycode, out char keypadCode))
		{
			SendUserData(System.Text.Encoding.ASCII.GetBytes("\u001bO" + keypadCode));
			return;
		}
		SendUserData(TerminalKeyboard.Encode(
			terminalKey, text, key.CtrlPressed && !altGr, key.AltPressed && !altGr,
			key.ShiftPressed || key.Keycode == Key.Backtab, _buffer.ApplicationCursorKeys, _buffer.NewLineMode
		));
	}

	private static bool TryKeypad(Key key, out char code)
	{
		code = key switch
		{
			>= Key.Kp0 and <= Key.Kp9 => (char)('p' + key - Key.Kp0),
			Key.KpEnter => 'M', Key.KpAdd => 'k', Key.KpSubtract => 'm',
			Key.KpMultiply => 'j', Key.KpDivide => 'o', Key.KpPeriod => 'n', _ => '\0'
		};
		return code != '\0';
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (!InputEnabled) return;
		if (@event is InputEventMouseButton button)
		{
			if (button.ButtonIndex == MouseButton.Left)
			{
				if (button.Pressed)
				{
					GrabFocus();
					ResetBlink();
					_selectionStart = _selectionEnd = CellAt(button.Position);
					_hasSelection = false;
				}
				_selecting = button.Pressed;
				AcceptEvent();
			}
			else if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
			{
				ScrollHistory(button.ButtonIndex == MouseButton.WheelUp ? 3 : -3);
				AcceptEvent();
			}
		}
		else if (_selecting && @event is InputEventMouseMotion motion)
		{
			_selectionEnd = CellAt(motion.Position);
			_hasSelection = _selectionStart != _selectionEnd;
			AcceptEvent();
		}
		QueueRedraw();
	}

	private Vector2I CellAt(Vector2 position)
	{
		Vector2 cell = (position - _padding) / _cellSize;
		return new Vector2I(Math.Clamp((int)cell.X, 0, Columns - 1),
			Math.Clamp((int)cell.Y, 0, Rows - 1) + _buffer.HistoryCount - _scrollOffset);
	}

	private void ScrollHistory(int count)
	{
		_scrollOffset = Math.Clamp(_scrollOffset + count, 0, _buffer.HistoryCount);
		QueueRedraw();
	}

	private void SendUserData(byte[] data)
	{
		if (data.Length == 0) return;
		_scrollOffset = 0;
		_hasSelection = false;
		ResetBlink();
		SendData(data);
	}

	private void SendData(byte[] data)
	{
		if (InputEnabled) EmitSignal(SignalName.DataSent, data);
	}

	private void ResetBlink()
	{
		_blinkTime = 0;
		_cursorOn = true;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!IsVisibleInTree() || !HasFocus() || !InputEnabled) return;
		_blinkTime += delta;
		if (_blinkTime >= 0.5)
		{
			_blinkTime = 0;
			_cursorOn = !_cursorOn;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (_normalStyle == null) return;
		DrawStyleBox(_normalStyle, new Rect2(Vector2.Zero, Size));
		if (HasFocus()) DrawStyleBox(_focusStyle, new Rect2(Vector2.Zero, Size));
		for (int row = 0; row < Rows; row++)
		{
			for (int column = 0; column < Columns; column++)
			{
				TerminalCell cell = _buffer.GetCell(column, row, _scrollOffset);
				if (cell.Width == 0) continue;
				Color foreground = ResolveColor(cell.Foreground, _foreground);
				Color background = ResolveColor(cell.Background, _background);
				if (cell.Attributes.HasFlag(TerminalAttributes.Inverse)) (foreground, background) = (background, foreground);
				bool selected = IsSelected(column, row);
				bool cursor = InputEnabled && _buffer.CursorVisible && _scrollOffset == 0 && row == _buffer.CursorRow && column == _buffer.CursorColumn;
				if (selected) background = new Color(0.12f, 0.35f, 0.4f);
				if (cursor && HasFocus() && _cursorOn) (foreground, background) = (_background, _caret);
				Vector2 position = _padding + new Vector2(column * _cellSize.X, row * _cellSize.Y);
				var rectangle = new Rect2(position, new Vector2(_cellSize.X * cell.Width, _cellSize.Y));
				if (background != _background) DrawRect(rectangle, background);
				if (cell.Attributes.HasFlag(TerminalAttributes.Dim)) foreground = foreground.Darkened(0.4f);
				if (!cell.Attributes.HasFlag(TerminalAttributes.Hidden) && !string.IsNullOrWhiteSpace(cell.Text))
				{
					int font = (cell.Attributes.HasFlag(TerminalAttributes.Bold) ? 1 : 0) + (cell.Attributes.HasFlag(TerminalAttributes.Italic) ? 2 : 0);
					DrawString(_fonts[font], position + new Vector2(0, _ascent), cell.Text, fontSize: _fontSize, modulate: foreground);
				}
				if (cell.Attributes.HasFlag(TerminalAttributes.Underline))
					DrawLine(position + new Vector2(0, _ascent + 2), position + new Vector2(rectangle.Size.X, _ascent + 2), foreground);
				if (cell.Attributes.HasFlag(TerminalAttributes.Strike))
					DrawLine(position + new Vector2(0, _ascent * 0.6f), position + new Vector2(rectangle.Size.X, _ascent * 0.6f), foreground);
				if (cursor && !HasFocus()) DrawRect(rectangle, _caret, false);
			}
		}
	}

	private bool IsSelected(int column, int row)
	{
		if (!_hasSelection) return false;
		long current = (long)(_buffer.HistoryCount - _scrollOffset + row) * Columns + column;
		long start = (long)_selectionStart.Y * Columns + _selectionStart.X;
		long end = (long)_selectionEnd.Y * Columns + _selectionEnd.X;
		return current >= Math.Min(start, end) && current <= Math.Max(start, end);
	}

	private static Color ResolveColor(int color, Color fallback)
	{
		if (color < 0) return fallback;
		if (color >= 0x1000000)
			return new Color(((color >> 16) & 255) / 255f, ((color >> 8) & 255) / 255f, (color & 255) / 255f);
		if (color >= 232)
		{
			float gray = (8 + (color - 232) * 10) / 255f;
			return new Color(gray, gray, gray);
		}
		if (color >= 16)
		{
			int value = color - 16;
			return new Color(CubeChannel(value / 36), CubeChannel(value / 6 % 6), CubeChannel(value % 6));
		}
		if (color is 7 or 15) return color == 7 ? new Color(0.75f, 0.75f, 0.75f) : Colors.White;
		if (color == 8) return new Color(0.5f, 0.5f, 0.5f);
		float intensity = color >= 8 ? 1f : 0.65f;
		float low = color >= 8 ? 0.25f : 0f;
		return new Color((color & 1) != 0 ? intensity : low, (color & 2) != 0 ? intensity : low, (color & 4) != 0 ? intensity : low);
	}

	private static float CubeChannel(int value) => value == 0 ? 0 : (55 + value * 40) / 255f;
}
