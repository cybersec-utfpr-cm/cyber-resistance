using Godot;

public partial class PauseMenu : CanvasLayer
{
	private HSlider _masterSlider;
	private HSlider _musicSlider;
	private HSlider _ambienceSlider;
	private HSlider _effectsSlider;
	private Label _masterValue;
	private Label _musicValue;
	private Label _ambienceValue;
	private Label _effectsValue;
	private Button _resumeButton;
	private Button _mainMenuButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_masterSlider = GetNode<HSlider>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Master/Slider"
		);
		_musicSlider = GetNode<HSlider>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Music/Slider"
		);
		_ambienceSlider = GetNode<HSlider>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Ambience/Slider"
		);
		_effectsSlider = GetNode<HSlider>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Effects/Slider"
		);
		_masterValue = GetNode<Label>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Master/Header/Value"
		);
		_musicValue = GetNode<Label>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Music/Header/Value"
		);
		_ambienceValue = GetNode<Label>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Ambience/Header/Value"
		);
		_effectsValue = GetNode<Label>(
			"Center/PausePanel/PauseMargin/Content/AudioPanel/AudioMargin/AudioSettings/Effects/Header/Value"
		);
		_resumeButton = GetNode<Button>(
			"Center/PausePanel/PauseMargin/Content/Actions/ResumeButton"
		);
		_mainMenuButton = GetNode<Button>(
			"Center/PausePanel/PauseMargin/Content/Actions/MainMenuButton"
		);

		InitializeVolumes();
		_masterSlider.ValueChanged += OnMasterVolumeChanged;
		_musicSlider.ValueChanged += OnMusicVolumeChanged;
		_ambienceSlider.ValueChanged += OnAmbienceVolumeChanged;
		_effectsSlider.ValueChanged += OnEffectsVolumeChanged;
		_resumeButton.Pressed += OnResumePressed;
		_mainMenuButton.Pressed += OnMainMenuPressed;
		_resumeButton.GrabFocus();
	}

	private void InitializeVolumes()
	{
		var audio = AudioManager.Instance;

		if (audio == null)
			return;

		SetSliderValue(_masterSlider, _masterValue, audio.MasterVolume);
		SetSliderValue(_musicSlider, _musicValue, audio.MusicVolume);
		SetSliderValue(
			_ambienceSlider,
			_ambienceValue,
			audio.AmbienceVolume
		);
		SetSliderValue(_effectsSlider, _effectsValue, audio.EffectsVolume);
	}

	private static void SetSliderValue(
		HSlider slider,
		Label valueLabel,
		float normalizedValue
	)
	{
		double percentage = Mathf.Round(normalizedValue * 100.0f);
		slider.SetValueNoSignal(percentage);
		valueLabel.Text = $"{percentage:0}%";
	}

	private void OnMasterVolumeChanged(double value)
	{
		UpdateValueLabel(_masterValue, value);
		AudioManager.Instance?.SetMasterVolume((float)value / 100.0f);
	}

	private void OnMusicVolumeChanged(double value)
	{
		UpdateValueLabel(_musicValue, value);
		AudioManager.Instance?.SetMusicVolume((float)value / 100.0f);
	}

	private void OnAmbienceVolumeChanged(double value)
	{
		UpdateValueLabel(_ambienceValue, value);
		AudioManager.Instance?.SetAmbienceVolume((float)value / 100.0f);
	}

	private void OnEffectsVolumeChanged(double value)
	{
		UpdateValueLabel(_effectsValue, value);
		AudioManager.Instance?.SetEffectsVolume((float)value / 100.0f);
	}

	private static void UpdateValueLabel(Label label, double value)
	{
		label.Text = $"{Mathf.Round(value):0}%";
	}

	private void OnResumePressed()
	{
		PauseManager.Instance?.ResumeGame();
	}

	private void OnMainMenuPressed()
	{
		PauseManager.Instance?.ReturnToMainMenu();
	}
}
