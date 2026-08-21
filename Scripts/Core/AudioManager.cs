using Godot;
using System.Collections.Generic;

public partial class AudioManager : Node
{
	private const string SettingsPath = "user://audio_settings.cfg";
	private const string AudioSection = "audio";
	private const string MusicBus = "Music";
	private const string AmbienceBus = "Ambience";
	private const string EffectsBus = "SFX";
	private const string MainMenuScenePath =
		"res://Scenes/Interfaces/main_menu.tscn";
	private const string MainMenuMusicPath =
		"res://Assets/Audio/Music/menu_theme.ogg";
	private const string GameplayMusicPath =
		"res://Assets/Audio/Music/gameplay_theme.ogg";
	private const string WorldAmbiencePath =
		"res://Assets/Audio/Ambience/world_ambience.ogg";
	private const string IndoorAmbiencePath =
		"res://Assets/Audio/Ambience/indoor_ambience.ogg";

	private const float DefaultMasterVolume = 0.8f;
	private const float DefaultMusicVolume = 0.55f;
	private const float DefaultAmbienceVolume = 0.5f;
	private const float DefaultEffectsVolume = 0.75f;

	private readonly Dictionary<string, AudioStreamPlayer> _effectPlayers =
		new();
	private AudioStreamPlayer _musicPlayer;
	private AudioStreamPlayer _ambiencePlayer;
	private string _currentMusicPath = "";
	private string _currentAmbiencePath = "";
	private bool _saveQueued;

	public static AudioManager Instance { get; private set; }

	public float MasterVolume { get; private set; } = DefaultMasterVolume;
	public float MusicVolume { get; private set; } = DefaultMusicVolume;
	public float AmbienceVolume { get; private set; } = DefaultAmbienceVolume;
	public float EffectsVolume { get; private set; } = DefaultEffectsVolume;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		EnsureAudioBuses();
		LoadSettings();
		CreateAudioPlayers();

		GetTree().NodeAdded += OnNodeAdded;
		HookButtonsRecursively(GetTree().Root);
		Callable.From(RefreshSceneContext).CallDeferred();
	}

	public override void _ExitTree()
	{
		if (GetTree() != null)
			GetTree().NodeAdded -= OnNodeAdded;

		if (Instance == this)
			Instance = null;
	}

	public void SetMasterVolume(float value)
	{
		MasterVolume = ApplyBusVolume("Master", value);
		QueueSettingsSave();
	}

	public void SetMusicVolume(float value)
	{
		MusicVolume = ApplyBusVolume(MusicBus, value);
		QueueSettingsSave();
	}

	public void SetAmbienceVolume(float value)
	{
		AmbienceVolume = ApplyBusVolume(AmbienceBus, value);
		QueueSettingsSave();
	}

	public void SetEffectsVolume(float value)
	{
		EffectsVolume = ApplyBusVolume(EffectsBus, value);
		QueueSettingsSave();
	}

	public void SetMenuContext()
	{
		PlayLoop(_musicPlayer, MainMenuMusicPath, ref _currentMusicPath);
		StopLoop(_ambiencePlayer, ref _currentAmbiencePath);
	}

	public void SetGameplayContext(string environmentScenePath)
	{
		PlayLoop(_musicPlayer, GameplayMusicPath, ref _currentMusicPath);

		string normalizedPath = environmentScenePath?.ToLowerInvariant() ?? "";
		string ambiencePath =
			normalizedPath.Contains("office") ||
			normalizedPath.Contains("cafeteria")
				? IndoorAmbiencePath
				: WorldAmbiencePath;

		PlayLoop(_ambiencePlayer, ambiencePath, ref _currentAmbiencePath);
	}

	public void PlayUiClick()
	{
		PlayEffect("ui_click");
	}

	public void PlayPauseOpen()
	{
		PlayEffect("pause_open");
	}

	public void PlayPauseClose()
	{
		PlayEffect("pause_close");
	}

	public void PlayDoor()
	{
		PlayEffect("door");
	}

	public void PlayInteraction()
	{
		PlayEffect("interaction");
	}

	public void PlayFootstep()
	{
		PlayEffect("footstep");
	}

	public void PlaySuccess()
	{
		PlayEffect("success");
	}

	public void PlayError()
	{
		PlayEffect("error");
	}

	private void EnsureAudioBuses()
	{
		EnsureBus(MusicBus);
		EnsureBus(AmbienceBus);
		EnsureBus(EffectsBus);
	}

	private static void EnsureBus(string busName)
	{
		if (AudioServer.GetBusIndex(busName) >= 0)
			return;

		AudioServer.AddBus();
		int busIndex = AudioServer.BusCount - 1;
		AudioServer.SetBusName(busIndex, busName);
		AudioServer.SetBusSend(busIndex, "Master");
	}

	private void LoadSettings()
	{
		var config = new ConfigFile();

		if (config.Load(SettingsPath) == Error.Ok)
		{
			MasterVolume = ReadVolume(
				config,
				"master",
				DefaultMasterVolume
			);
			MusicVolume = ReadVolume(
				config,
				"music",
				DefaultMusicVolume
			);
			AmbienceVolume = ReadVolume(
				config,
				"ambience",
				DefaultAmbienceVolume
			);
			EffectsVolume = ReadVolume(
				config,
				"effects",
				DefaultEffectsVolume
			);
		}

		MasterVolume = ApplyBusVolume("Master", MasterVolume);
		MusicVolume = ApplyBusVolume(MusicBus, MusicVolume);
		AmbienceVolume = ApplyBusVolume(AmbienceBus, AmbienceVolume);
		EffectsVolume = ApplyBusVolume(EffectsBus, EffectsVolume);
	}

	private static float ReadVolume(
		ConfigFile config,
		string key,
		float defaultValue
	)
	{
		return Mathf.Clamp(
			config.GetValue(AudioSection, key, defaultValue).AsSingle(),
			0.0f,
			1.0f
		);
	}

	private static float ApplyBusVolume(string busName, float value)
	{
		float normalizedValue = Mathf.Clamp(value, 0.0f, 1.0f);
		int busIndex = AudioServer.GetBusIndex(busName);

		if (busIndex < 0)
			return normalizedValue;

		bool muted = normalizedValue <= 0.001f;
		AudioServer.SetBusMute(busIndex, muted);
		AudioServer.SetBusVolumeDb(
			busIndex,
			muted ? -80.0f : Mathf.LinearToDb(normalizedValue)
		);

		return normalizedValue;
	}

	private void QueueSettingsSave()
	{
		if (_saveQueued)
			return;

		_saveQueued = true;
		Callable.From(SaveSettings).CallDeferred();
	}

	private void SaveSettings()
	{
		_saveQueued = false;
		var config = new ConfigFile();
		config.SetValue(AudioSection, "master", MasterVolume);
		config.SetValue(AudioSection, "music", MusicVolume);
		config.SetValue(AudioSection, "ambience", AmbienceVolume);
		config.SetValue(AudioSection, "effects", EffectsVolume);

		Error result = config.Save(SettingsPath);

		if (result != Error.Ok)
			GD.PrintErr($"AudioManager: falha ao salvar volumes: {result}.");
	}

	private void CreateAudioPlayers()
	{
		_musicPlayer = CreatePlayer("MusicPlayer", MusicBus);
		_ambiencePlayer = CreatePlayer("AmbiencePlayer", AmbienceBus);

		CreateEffectPlayer(
			"ui_click",
			"res://Assets/Audio/SFX/ui_click.wav"
		);
		CreateEffectPlayer(
			"pause_open",
			"res://Assets/Audio/SFX/pause_open.wav"
		);
		CreateEffectPlayer(
			"pause_close",
			"res://Assets/Audio/SFX/pause_close.wav"
		);
		CreateEffectPlayer("door", "res://Assets/Audio/SFX/door.wav");
		CreateEffectPlayer(
			"interaction",
			"res://Assets/Audio/SFX/interaction.wav"
		);
		CreateEffectPlayer(
			"footstep",
			"res://Assets/Audio/SFX/footstep.wav"
		);
		CreateEffectPlayer(
			"success",
			"res://Assets/Audio/SFX/success.wav"
		);
		CreateEffectPlayer("error", "res://Assets/Audio/SFX/error.wav");
	}

	private AudioStreamPlayer CreatePlayer(string playerName, string busName)
	{
		var player = new AudioStreamPlayer
		{
			Name = playerName,
			Bus = busName,
			ProcessMode = ProcessModeEnum.Always
		};

		AddChild(player);
		return player;
	}

	private void CreateEffectPlayer(string effectName, string streamPath)
	{
		AudioStream stream = GD.Load<AudioStream>(streamPath);

		if (stream == null)
		{
			GD.PrintErr($"AudioManager: áudio não encontrado: {streamPath}.");
			return;
		}

		var player = CreatePlayer($"Sfx_{effectName}", EffectsBus);
		player.Stream = stream;
		_effectPlayers[effectName] = player;
	}

	private void PlayEffect(string effectName)
	{
		if (!_effectPlayers.TryGetValue(effectName, out var player))
			return;

		player.Play();
	}

	private static void EnableLoop(AudioStream stream)
	{
		if (stream is AudioStreamOggVorbis oggStream)
			oggStream.Loop = true;
		else if (stream is AudioStreamWav wavStream)
			wavStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
	}

	private static void PlayLoop(
		AudioStreamPlayer player,
		string streamPath,
		ref string currentPath
	)
	{
		if (player == null || (currentPath == streamPath && player.Playing))
			return;

		AudioStream stream = GD.Load<AudioStream>(streamPath);

		if (stream == null)
		{
			GD.PrintErr($"AudioManager: áudio não encontrado: {streamPath}.");
			return;
		}

		EnableLoop(stream);
		player.Stream = stream;
		player.Play();
		currentPath = streamPath;
	}

	private static void StopLoop(
		AudioStreamPlayer player,
		ref string currentPath
	)
	{
		player?.Stop();
		currentPath = "";
	}

	private void RefreshSceneContext()
	{
		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? "";

		if (scenePath == MainMenuScenePath)
			SetMenuContext();
		else if (scenePath.EndsWith("/game.tscn"))
			SetGameplayContext("world");
	}

	private void OnNodeAdded(Node node)
	{
		if (node is Button button)
			HookButton(button);

		if (
			node.GetParent() == GetTree().Root &&
			!string.IsNullOrEmpty(node.SceneFilePath)
		)
		{
			Callable.From(RefreshSceneContext).CallDeferred();
		}
	}

	private void HookButtonsRecursively(Node node)
	{
		if (node is Button button)
			HookButton(button);

		foreach (Node child in node.GetChildren())
			HookButtonsRecursively(child);
	}

	private void HookButton(Button button)
	{
		if (button.HasMeta("audio_manager_hooked"))
			return;

		button.SetMeta("audio_manager_hooked", true);
		button.Pressed += PlayUiClick;
	}
}
