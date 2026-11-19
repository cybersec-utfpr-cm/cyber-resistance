using Godot;

public partial class MissionSubmissionUi : CanvasLayer
{
	[Signal]
	public delegate void AnswerSelectedEventHandler(bool answeredYes);

	[Signal]
	public delegate void FlagSubmittedEventHandler(string candidate);

	[Signal]
	public delegate void CancelledEventHandler();

	private Label _titleLabel;
	private Label _promptLabel;
	private Label _feedbackLabel;
	private HBoxContainer _questionActions;
	private VBoxContainer _inputSection;
	private Button _yesButton;
	private Button _noButton;
	private LineEdit _flagInput;
	private Button _sendButton;
	private Button _cancelButton;
	private QuestLogUi _questLog;
	private bool _questLogWasObscured;
	private bool _wasTreePaused;
	private bool _modalStateCaptured;

	public override void _Ready()
	{
		_titleLabel = GetNodeOrNull<Label>(
			"Root/Center/Panel/Margin/Content/Title"
		);
		_promptLabel = GetNodeOrNull<Label>(
			"Root/Center/Panel/Margin/Content/Prompt"
		);
		_feedbackLabel = GetNodeOrNull<Label>(
			"Root/Center/Panel/Margin/Content/Feedback"
		);
		_questionActions = GetNodeOrNull<HBoxContainer>(
			"Root/Center/Panel/Margin/Content/QuestionActions"
		);
		_inputSection = GetNodeOrNull<VBoxContainer>(
			"Root/Center/Panel/Margin/Content/InputSection"
		);
		_noButton = GetNodeOrNull<Button>(
			"Root/Center/Panel/Margin/Content/QuestionActions/NoButton"
		);
		_yesButton = GetNodeOrNull<Button>(
			"Root/Center/Panel/Margin/Content/QuestionActions/YesButton"
		);
		_flagInput = GetNodeOrNull<LineEdit>(
			"Root/Center/Panel/Margin/Content/InputSection/FlagInput"
		);
		_cancelButton = GetNodeOrNull<Button>(
			"Root/Center/Panel/Margin/Content/InputSection/InputActions/CancelButton"
		);
		_sendButton = GetNodeOrNull<Button>(
			"Root/Center/Panel/Margin/Content/InputSection/InputActions/SendButton"
		);

		if (
			_titleLabel == null ||
			_promptLabel == null ||
			_feedbackLabel == null ||
			_questionActions == null ||
			_inputSection == null ||
			_noButton == null ||
			_yesButton == null ||
			_flagInput == null ||
			_cancelButton == null ||
			_sendButton == null
		)
		{
			GD.PrintErr(
				"MissionSubmissionUi: estrutura da interface não encontrada."
			);
			QueueFree();
			return;
		}

		_noButton.Pressed += OnNoPressed;
		_yesButton.Pressed += OnYesPressed;
		_cancelButton.Pressed += OnCancelPressed;
		_sendButton.Pressed += OnSendPressed;
		_flagInput.TextSubmitted += OnTextSubmitted;
		Hide();
	}

	public override void _ExitTree()
	{
		RestoreModalState();

		if (_noButton != null)
			_noButton.Pressed -= OnNoPressed;
		if (_yesButton != null)
			_yesButton.Pressed -= OnYesPressed;
		if (_cancelButton != null)
			_cancelButton.Pressed -= OnCancelPressed;
		if (_sendButton != null)
			_sendButton.Pressed -= OnSendPressed;
		if (_flagInput != null)
			_flagInput.TextSubmitted -= OnTextSubmitted;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			Visible &&
			@event.IsActionPressed("ui_cancel") &&
			!@event.IsEcho()
		)
		{
			EmitSignal(SignalName.Cancelled);
			GetViewport().SetInputAsHandled();
		}
	}

	public void ShowQuestion(string title, string prompt)
	{
		CaptureModalState();
		_titleLabel.Text = title;
		_promptLabel.Text = prompt;
		_questionActions.Visible = true;
		_inputSection.Visible = false;
		_feedbackLabel.Visible = false;
		_feedbackLabel.Text = "";
		_flagInput.Text = "";
		_sendButton.Disabled = false;
		Show();
		_yesButton.GrabFocus();
	}

	public void ShowFlagInput()
	{
		_promptLabel.Text = "Digite a flag encontrada no laboratório:";
		_questionActions.Visible = false;
		_inputSection.Visible = true;
		_feedbackLabel.Visible = false;
		_feedbackLabel.Text = "";
		_flagInput.Text = "";
		_sendButton.Disabled = false;
		_flagInput.GrabFocus();
	}

	public void ShowSubmissionError(string message)
	{
		_feedbackLabel.Text = message;
		_feedbackLabel.Visible = true;
		_sendButton.Disabled = false;
		_flagInput.SelectAll();
		_flagInput.GrabFocus();
	}

	public void CloseUi()
	{
		_flagInput.Text = "";
		Hide();
		RestoreModalState();
		QueueFree();
	}

	private void OnNoPressed()
	{
		EmitSignal(SignalName.AnswerSelected, false);
	}

	private void OnYesPressed()
	{
		EmitSignal(SignalName.AnswerSelected, true);
	}

	private void OnCancelPressed()
	{
		EmitSignal(SignalName.Cancelled);
	}

	private void OnSendPressed()
	{
		SubmitCurrentText();
	}

	private void OnTextSubmitted(string candidate)
	{
		SubmitCurrentText();
	}

	private void SubmitCurrentText()
	{
		if (_sendButton.Disabled)
			return;

		_sendButton.Disabled = true;
		EmitSignal(SignalName.FlagSubmitted, _flagInput.Text);
	}

	private void CaptureModalState()
	{
		if (_modalStateCaptured)
			return;

		_questLog =
			GetTree().GetFirstNodeInGroup("quest_log_ui") as QuestLogUi;
		if (_questLog != null)
		{
			_questLogWasObscured = _questLog.IsModalObscured;
			_questLog.SetModalObscured(true);
		}

		_wasTreePaused = GetTree().Paused;
		GetTree().Paused = true;
		_modalStateCaptured = true;
	}

	private void RestoreModalState()
	{
		if (!_modalStateCaptured)
			return;

		if (
			GodotObject.IsInstanceValid(_questLog) &&
			!_questLogWasObscured
		)
		{
			_questLog.SetModalObscured(false);
		}

		if (!_wasTreePaused && GetTree() != null)
			GetTree().Paused = false;

		_questLog = null;
		_modalStateCaptured = false;
	}
}
