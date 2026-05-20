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
	}

	private void OnNetworkSelected(long index)
	{
		_selectedNetwork = _networkList.GetItemText((int)index);
		_connectButton.Disabled = string.IsNullOrEmpty(_selectedNetwork);
	}

	private void OnConnectPressed()
	{
		if (_selectedNetwork != OfficeNetworkName)
		{
			_statusLabel.Text = "Rede não disponível para conexão.";
			return;
		}

		int stage = QuestManager.Instance.GetQuestStage("wifi_hacking");
		if (stage == 1)
			QuestManager.Instance.SetQuestStage("wifi_hacking", 2);

		_networkList.Visible = false;
		_connectButton.Visible = false;
		_anagram.Visible = true;
		_anagram.Restart();
		_statusLabel.Text = "";
	}

	private async void OnAnagramSuccess()
	{
		// Avança para estágio 3 (descobriu a senha)
		int stage = QuestManager.Instance.GetQuestStage("wifi_hacking");
		if (stage == 2)
		{
			QuestManager.Instance.SetQuestStage("wifi_hacking", 3);
		}

		// Mostra mensagem de sucesso
		_statusLabel.Text = "Conexão estabelecida com sucesso!";
		_anagram.Visible = false;

		// Aguarda 3 segundos
		await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);

		// Conclui a missão (estágio 4: conectado)
		QuestManager.Instance.CompleteQuest("wifi_hacking");
		GD.Print("WiFiScreen: Missão wifi_hacking concluída!");

		// Retorna à lista de redes (agora já conectado)
		_networkList.Visible = true;
		_connectButton.Visible = true;
		_statusLabel.Text = "Conectado à rede Escritório.";
		_connectButton.Disabled = true; // não permitir reconectar
	}
	private void _on_back_icon_bt_pressed() {
		GetParent<Screens>().ShowScreen("Settings");
	}
}
