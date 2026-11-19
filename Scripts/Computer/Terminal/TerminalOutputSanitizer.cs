using System;
using System.Text;

public sealed class TerminalOutputSanitizer
{
	private const string VisibleShellPrefix = "]3008;";

	private enum ControlState
	{
		Text,
		Escape,
		Csi,
		Osc,
		OscEscape,
		VisibleShellPrefix,
		VisibleShellSequence
	}

	private readonly StringBuilder _visibleShellBuffer = new();
	private ControlState _state = ControlState.Text;
	private int _visibleShellPrefixIndex;
	private bool _previousWasCarriageReturn;

	public string Process(string output)
	{
		if (string.IsNullOrEmpty(output))
			return string.Empty;

		var sanitized = new StringBuilder(output.Length);

		foreach (char value in output)
			ProcessCharacter(value, sanitized);

		return sanitized.ToString();
	}

	public void Reset()
	{
		_state = ControlState.Text;
		_visibleShellBuffer.Clear();
		_visibleShellPrefixIndex = 0;
		_previousWasCarriageReturn = false;
	}

	private void ProcessCharacter(char value, StringBuilder output)
	{
		switch (_state)
		{
			case ControlState.Text:
				ProcessTextCharacter(value, output);
				break;

			case ControlState.Escape:
				ProcessEscapeCharacter(value);
				break;

			case ControlState.Csi:
				ProcessCsiCharacter(value);
				break;

			case ControlState.Osc:
				ProcessOscCharacter(value);
				break;

			case ControlState.OscEscape:
				ProcessOscEscapeCharacter(value);
				break;

			case ControlState.VisibleShellPrefix:
				ProcessVisibleShellPrefixCharacter(value, output);
				break;

			case ControlState.VisibleShellSequence:
				ProcessVisibleShellSequenceCharacter(value, output);
				break;
		}
	}

	private void ProcessTextCharacter(char value, StringBuilder output)
	{
		if (value == '\x1B')
		{
			_state = ControlState.Escape;
			return;
		}

		if (value == '\u009B')
		{
			_state = ControlState.Csi;
			return;
		}

		if (value == '\u009D')
		{
			_state = ControlState.Osc;
			return;
		}

		if (value == ']')
		{
			_visibleShellBuffer.Clear();
			_visibleShellBuffer.Append(value);
			_visibleShellPrefixIndex = 1;
			_state = ControlState.VisibleShellPrefix;
			return;
		}

		AppendVisibleCharacter(value, output);
	}

	private void ProcessEscapeCharacter(char value)
	{
		_state = value switch
		{
			'[' => ControlState.Csi,
			']' => ControlState.Osc,
			'\x1B' => ControlState.Escape,
			_ => ControlState.Text
		};
	}

	private void ProcessCsiCharacter(char value)
	{
		if (value == '\x1B')
		{
			_state = ControlState.Escape;
			return;
		}

		if (value >= '@' && value <= '~')
			_state = ControlState.Text;
	}

	private void ProcessOscCharacter(char value)
	{
		if (value is '\x07' or '\u009C')
		{
			_state = ControlState.Text;
			return;
		}

		if (value == '\x1B')
			_state = ControlState.OscEscape;
	}

	private void ProcessOscEscapeCharacter(char value)
	{
		if (value == '\\' || value == '\u009C')
		{
			_state = ControlState.Text;
			return;
		}

		_state = value == '\x1B'
			? ControlState.OscEscape
			: ControlState.Osc;
	}

	private void ProcessVisibleShellPrefixCharacter(
		char value,
		StringBuilder output
	)
	{
		if (
			_visibleShellPrefixIndex < VisibleShellPrefix.Length &&
			value == VisibleShellPrefix[_visibleShellPrefixIndex]
		)
		{
			_visibleShellBuffer.Append(value);
			_visibleShellPrefixIndex++;

			if (_visibleShellPrefixIndex == VisibleShellPrefix.Length)
				_state = ControlState.VisibleShellSequence;

			return;
		}

		AppendVisibleBuffer(output);
		_state = ControlState.Text;
		ProcessCharacter(value, output);
	}

	private void ProcessVisibleShellSequenceCharacter(
		char value,
		StringBuilder output
	)
	{
		if (value is '\x07' or '\\')
		{
			_visibleShellBuffer.Clear();
			_visibleShellPrefixIndex = 0;
			_state = ControlState.Text;
			return;
		}

		if (value is '\r' or '\n')
		{
			AppendVisibleBuffer(output);
			_state = ControlState.Text;
			ProcessCharacter(value, output);
			return;
		}

		_visibleShellBuffer.Append(value);
	}

	private void AppendVisibleBuffer(StringBuilder output)
	{
		foreach (char value in _visibleShellBuffer.ToString())
			AppendVisibleCharacter(value, output);

		_visibleShellBuffer.Clear();
		_visibleShellPrefixIndex = 0;
	}

	private void AppendVisibleCharacter(char value, StringBuilder output)
	{
		if (value == '\r')
		{
			output.Append('\n');
			_previousWasCarriageReturn = true;
			return;
		}

		if (value == '\n')
		{
			if (!_previousWasCarriageReturn)
				output.Append(value);

			_previousWasCarriageReturn = false;
			return;
		}

		if (value == '\t')
		{
			output.Append(value);
			_previousWasCarriageReturn = false;
			return;
		}

		if (char.IsControl(value))
			return;

		output.Append(value);
		_previousWasCarriageReturn = false;
	}
}
