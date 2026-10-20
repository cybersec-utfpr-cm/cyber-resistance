using Godot;

/// Tela para subir o servidor central (port direto de server_main.gd).
public partial class ServerMain : Control
{
	private LineEdit _ipLine;
	private LineEdit _portLine;
	private Button _startButton;
	private Label _statusLabel;

	public override void _Ready()
	{
		_ipLine = GetNode<LineEdit>("VBox/IpRow/IpLine");
		_portLine = GetNode<LineEdit>("VBox/PortRow/PortLine");
		_startButton = GetNode<Button>("VBox/StartButton");
		_statusLabel = GetNode<Label>("VBox/StatusLabel");

		_startButton.Pressed += OnStartPressed;
		_ipLine.Text = GuessLocalIpv4();
	}

	private string GuessLocalIpv4()
	{
		foreach (string ip in IP.GetLocalAddresses())
		{
			if (
				ip.Count(".") == 3
				&& (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
			)
			{
				return ip;
			}
		}
		return "";
	}

	private void OnStartPressed()
	{
		string ip = _ipLine.Text.Trim();
		if (ip.Length == 0)
		{
			_statusLabel.Text = "Status: informe o IP";
			return;
		}
		string portText = _portLine.Text.Trim();
		if (!int.TryParse(portText, out int port))
		{
			_statusLabel.Text = "Status: porta invalida";
			return;
		}
		if (port < 1 || port > 65535)
		{
			_statusLabel.Text = "Status: porta fora do intervalo (1-65535)";
			return;
		}
		_startButton.Disabled = true;
		if (ServerNetworkHandler.Instance.StartServer(ip, port))
		{
			_statusLabel.Text = $"Status: rodando em {ip}:{port}";
			_ipLine.Editable = false;
			_portLine.Editable = false;
		}
		else
		{
			_statusLabel.Text = "Status: falha ao iniciar (IP/porta invalidos?)";
			_startButton.Disabled = false;
		}
	}
}
