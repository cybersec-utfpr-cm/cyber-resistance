using Godot;
using System.Threading.Tasks;

public partial class WiFiScreen : Control
{
	[Export] public string OfficeNetworkName = "Escritório";

	private ItemList _networkList;
	private Button _connectButton;
	private Label _statusLabel;
	private Anagram _anagram;

	private string _selectedNetwork;
	private bool _isConnected;
	private bool _isCompletingConnection;

	public override void _Ready()
	{
		_networkList = GetNode<ItemList>("NetworkList");
		_connectButton = GetNode<Button>("ConnectButton");
		_statusLabel = GetNode<Label>("StatusLabel");
		_anagram = GetNode<Anagram>("Anagram");

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
		_connectButton.Disabled = string.IsNullOrEmpty(_selectedNetwork);
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
				_statusLabel.Text = "Rede não disponível para conexão.";
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
			_statusLabel.Text =
				"Conclua as etapas anteriores para acessar este desafio.";
			_connectButton.Disabled = true;
			return;
		}

		if (_selectedNetwork != OfficeNetworkName)
		{
			_statusLabel.Text = "Rede não disponível para conexão.";
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
		_statusLabel.Text = "Senha correta. Estabelecendo conexão...";

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

		if (QuestManager.Instance == null)
		{
			_connectButton.Disabled = true;
			_statusLabel.Text = "O sistema de missões não está disponível.";
			return;
		}

		if (QuestManager.Instance.IsQuestCompleted("wifi_hacking"))
		{
			if (_isConnected)
			{
				_networkList.Visible = false;
				_connectButton.Disabled = false;
				_connectButton.Text = "Desconectar";
				_statusLabel.Text =
					$"Conectado à rede {OfficeNetworkName}.";
				return;
			}

			_connectButton.Disabled =
				string.IsNullOrEmpty(_selectedNetwork);
			_statusLabel.Text =
				"Nenhuma rede conectada. Selecione uma rede.";
			return;
		}

		if (!QuestManager.Instance.IsQuestActive("wifi_hacking"))
		{
			_connectButton.Disabled = true;
			_statusLabel.Text =
				"Conclua o tutorial para desbloquear este desafio.";
			return;
		}

		int stage = QuestManager.Instance.GetQuestStage("wifi_hacking");

		switch (stage)
		{
			case 1:
				_connectButton.Disabled =
					string.IsNullOrEmpty(_selectedNetwork);
				_statusLabel.Text =
					"Encontre e selecione a rede correta.";
				break;

			case 2:
				_networkList.Visible = false;
				_connectButton.Visible = false;
				_anagram.Visible = true;
				_anagram.Restart();
				_statusLabel.Text =
					"Descubra a senha para acessar a rede.";
				break;

			case 3:
				_ = CompleteConnectionAsync();
				break;

			default:
				_connectButton.Disabled = true;
				_statusLabel.Text = "Estado da missão inválido.";
				break;
		}
	}

	private void _on_back_icon_bt_pressed() {
		GetParent<Screens>().ShowScreen("Settings");
	}
}
