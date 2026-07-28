using Godot;
using System.Threading.Tasks;

public partial class WiFiScreen : Control
{
	[Export] public string OfficeNetworkName = "Escritório";

	private ItemList _networkList;
	private Button _connectButton;
	private Label _statusLabel;
	private Label _selectedNetworkLabel;
	private Label _scanStatus;
	private Anagram _anagram;

	private string _selectedNetwork;
	private bool _isConnected;
	private bool _isCompletingConnection;

	public override void _Ready()
	{
		const string detailsPath =
			"ContentMargin/MainContent/Columns/DetailsPanel/" +
			"DetailsMargin/DetailsContent";

		_networkList = GetNode<ItemList>(
			"ContentMargin/MainContent/Columns/NetworkPanel/" +
			"NetworkMargin/NetworkContent/NetworkList"
		);
		_scanStatus = GetNode<Label>(
			"ContentMargin/MainContent/Columns/NetworkPanel/" +
			"NetworkMargin/NetworkContent/ScanStatus"
		);
		_connectButton = GetNode<Button>(
			$"{detailsPath}/ConnectButton"
		);
		_statusLabel = GetNode<Label>(
			$"{detailsPath}/StatusLabel"
		);
		_selectedNetworkLabel = GetNode<Label>(
			$"{detailsPath}/SelectedNetworkLabel"
		);
		_anagram = GetNode<Anagram>(
			$"{detailsPath}/Anagram"
		);

		_networkList.AddItem(OfficeNetworkName);
		_networkList.AddItem("Convidados");
		_networkList.AddItem("Vizinhança");

		_networkList.ItemSelected += OnNetworkSelected;
		_connectButton.Pressed += OnConnectPressed;
		_connectButton.Disabled = true;

		_anagram.Visible = false;
		_anagram.Success += OnAnagramSuccess;

		_isConnected =
			SaveManager.Instance?.IsOfficeWifiConnected ?? false;

		if (_isConnected)
			_selectedNetwork = OfficeNetworkName;

		RefreshQuestState();
	}

	private void OnNetworkSelected(long index)
	{
		if (
			QuestManager.Instance == null ||
			(
				!QuestManager.Instance.IsQuestCompleted("wifi_hacking") &&
				QuestManager.Instance.GetQuestStage("wifi_hacking") != 1
			)
		)
			return;

		_selectedNetwork = _networkList.GetItemText((int)index);
		_selectedNetworkLabel.Text = _selectedNetwork;
		_connectButton.Disabled = string.IsNullOrEmpty(_selectedNetwork);

		SetStatus(
			"Rede selecionada. Pronta para iniciar a conexão.",
			VisualState.Info
		);
	}

	private void OnConnectPressed()
	{
		if (
			QuestManager.Instance != null &&
			QuestManager.Instance.IsQuestCompleted("wifi_hacking")
		)
		{
			if (_isConnected)
			{
				_isConnected = false;
				_selectedNetwork = string.Empty;
				_networkList.DeselectAll();
				SaveManager.Instance?.SetOfficeWifiConnected(false);
				RefreshQuestState();
				return;
			}

			if (_selectedNetwork != OfficeNetworkName)
			{
				SetStatus(
					"Esta rede não está disponível para conexão.",
					VisualState.Error
				);
				return;
			}

			_isConnected = true;
			SaveManager.Instance?.SetOfficeWifiConnected(true);
			RefreshQuestState();
			return;
		}

		if (
			QuestManager.Instance == null ||
			!QuestManager.Instance.IsQuestActive("wifi_hacking") ||
			QuestManager.Instance.GetQuestStage("wifi_hacking") != 1
		)
		{
			SetStatus(
				"Conclua as etapas anteriores para acessar este desafio.",
				VisualState.Warning
			);
			_connectButton.Disabled = true;
			return;
		}

		if (_selectedNetwork != OfficeNetworkName)
		{
			SetStatus(
				"Esta rede não está disponível para conexão.",
				VisualState.Error
			);
			return;
		}

		QuestManager.Instance.SetQuestStage("wifi_hacking", 2);
		RefreshQuestState();
	}

	private async void OnAnagramSuccess()
	{
		if (
			QuestManager.Instance == null ||
			!QuestManager.Instance.IsQuestActive("wifi_hacking") ||
			QuestManager.Instance.GetQuestStage("wifi_hacking") != 2
		)
		{
			RefreshQuestState();
			return;
		}

		QuestManager.Instance.SetQuestStage("wifi_hacking", 3);
		await CompleteConnectionAsync();
	}

	private async Task CompleteConnectionAsync()
	{
		if (_isCompletingConnection)
			return;

		_isCompletingConnection = true;
		_networkList.Visible = false;
		_connectButton.Visible = false;
		_anagram.Visible = false;
		_scanStatus.Text = "● Estabelecendo conexão";
		SetStatus(
			"Senha correta. Estabelecendo conexão segura...",
			VisualState.Info
		);

		await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);

		if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
			return;

		if (
			QuestManager.Instance != null &&
			QuestManager.Instance.IsQuestActive("wifi_hacking") &&
			QuestManager.Instance.GetQuestStage("wifi_hacking") == 3
		)
		{
			_isConnected = true;
			QuestManager.Instance.CompleteQuest("wifi_hacking");
			SaveManager.Instance?.SetOfficeWifiConnected(true);
			GD.Print("WiFiScreen: Missão wifi_hacking concluída.");
		}

		_isCompletingConnection = false;
		RefreshQuestState();
	}

	private void RefreshQuestState()
	{
		_networkList.Visible = true;
		_connectButton.Visible = true;
		_connectButton.Text = "Conectar";
		_anagram.Visible = false;
		_scanStatus.Text = "● 3 redes disponíveis";
		_selectedNetworkLabel.Text =
			string.IsNullOrEmpty(_selectedNetwork)
				? "Nenhuma rede"
				: _selectedNetwork;

		if (QuestManager.Instance == null)
		{
			_connectButton.Disabled = true;
			SetStatus(
				"O sistema de missões não está disponível.",
				VisualState.Error
			);
			return;
		}

		if (QuestManager.Instance.IsQuestCompleted("wifi_hacking"))
		{
			if (_isConnected)
			{
				_selectedNetwork = OfficeNetworkName;
				_selectedNetworkLabel.Text = OfficeNetworkName;
				_networkList.Visible = false;
				_scanStatus.Text = "● Conexão ativa";
				_connectButton.Disabled = false;
				_connectButton.Text = "Desconectar";
				SetStatus(
					$"Conectado à rede {OfficeNetworkName}.",
					VisualState.Success
				);
				return;
			}

			_connectButton.Disabled =
				string.IsNullOrEmpty(_selectedNetwork);
			SetStatus(
				"Nenhuma rede conectada. Selecione uma rede.",
				VisualState.Neutral
			);
			return;
		}

		if (!QuestManager.Instance.IsQuestActive("wifi_hacking"))
		{
			_connectButton.Disabled = true;
			SetStatus(
				"Conclua o tutorial para desbloquear este desafio.",
				VisualState.Warning
			);
			return;
		}

		int stage = QuestManager.Instance.GetQuestStage("wifi_hacking");

		switch (stage)
		{
			case 1:
				_connectButton.Disabled =
					string.IsNullOrEmpty(_selectedNetwork);
				SetStatus(
					"Encontre e selecione a rede correta.",
					VisualState.Info
				);
				break;

			case 2:
				_networkList.Visible = false;
				_connectButton.Visible = false;
				_anagram.Visible = true;
				_anagram.Restart();
				_scanStatus.Text = "● Desafio em andamento";
				SetStatus(
					"Descubra a senha para acessar a rede.",
					VisualState.Warning
				);
				break;

			case 3:
				_ = CompleteConnectionAsync();
				break;

			default:
				_connectButton.Disabled = true;
				SetStatus(
					"Estado da missão inválido.",
					VisualState.Error
				);
				break;
		}
	}

	private void SetStatus(string message, VisualState state)
	{
		_statusLabel.Text = message;
		_statusLabel.AddThemeColorOverride(
			"font_color",
			state switch
			{
				VisualState.Info =>
					new Color(0.337f, 0.82f, 0.867f, 1.0f),
				VisualState.Success =>
					new Color(0.384f, 0.839f, 0.545f, 1.0f),
				VisualState.Warning =>
					new Color(0.898f, 0.722f, 0.361f, 1.0f),
				VisualState.Error =>
					new Color(0.937f, 0.451f, 0.451f, 1.0f),
				_ =>
					new Color(0.58f, 0.698f, 0.749f, 1.0f)
			}
		);
	}

	private void _on_back_icon_bt_pressed()
	{
		GetParent<Screens>().ShowScreen("Settings");
	}

	private enum VisualState
	{
		Neutral,
		Info,
		Success,
		Warning,
		Error
	}
}
