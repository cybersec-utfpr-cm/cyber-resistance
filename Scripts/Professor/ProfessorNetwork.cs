using Godot;

// Cliente ENet do console do professor.
// Conecta ao server central (Scripts/Multiplayer/ServerNetworkHandler), se
// registra como professor com PROFESSOR_HELLO e converte os pacotes recebidos
// em sinais (eventos C#) para a UI. Roda como no da cena do professor, sem
// autoload.
public partial class ProfessorNetwork : Node
{
	public event System.Action ConnectedToServer;
	public event System.Action DisconnectedFromServer;
	public event System.Action<int> PeerIdReceived;
	public event System.Action<RefreshClass> RoomsReceived;
	public event System.Action<MinigameSubmissionPkt> SubmissionReceived;
	public event System.Action<string> StatusChanged;

	private ENetConnection _connection = null;
	private ENetPacketPeer _serverPeer = null;

	public bool IsConnectedToServer()
	{
		return _serverPeer != null && _serverPeer.GetState() == ENetPacketPeer.PeerState.Connected;
	}

	public Error ConnectToServer(string ip, int port)
	{
		DisconnectFromServer();
		_connection = new ENetConnection();
		// O servidor tambem usa a API de baixo nivel: host proprio + Service().
		Error error = _connection.CreateHost(32, 0, 0, 0);
		if (error != Error.Ok)
		{
			_connection = null;
			StatusChanged?.Invoke($"Falha ao criar socket local (erro {(int)error})");
			return error;
		}
		_serverPeer = _connection.ConnectToHost(ip, port);
		if (_serverPeer == null)
		{
			_connection.Destroy();
			_connection = null;
			StatusChanged?.Invoke($"Falha ao abrir conexao com {ip}:{port}");
			return Error.Failed;
		}
		StatusChanged?.Invoke($"Conectando a {ip}:{port} ...");
		return Error.Ok;
	}

	public void DisconnectFromServer()
	{
		bool wasConnected = IsConnectedToServer();
		if (_connection != null)
		{
			_connection.Destroy();
		}
		_connection = null;
		_serverPeer = null;
		if (wasConnected)
		{
			StatusChanged?.Invoke("Desconectado do servidor");
			DisconnectedFromServer?.Invoke();
		}
	}

	public void RequestRooms()
	{
		if (!SendOk())
		{
			return;
		}
		RefreshRequestClass.Create().Send(_serverPeer);
	}

	public bool SendGrade(int roomId, int teamIndex, float grade, string comment)
	{
		if (!SendOk())
		{
			return false;
		}
		MinigameGradePkt.Create(roomId, teamIndex, grade, comment).Send(_serverPeer);
		StatusChanged?.Invoke(
			$"Nota {grade:F1} enviada para a sala {roomId} (dupla {teamIndex})"
		);
		return true;
	}

	private bool SendOk()
	{
		if (!IsConnectedToServer())
		{
			StatusChanged?.Invoke("Sem conexao com o servidor");
			return false;
		}
		return true;
	}

	public override void _Process(double _delta)
	{
		if (_connection == null)
		{
			return;
		}
		ServiceEvents();
	}

	// Mesmo laço do server: consome todos os eventos disponiveis por frame.
	private void ServiceEvents()
	{
		Godot.Collections.Array ev = _connection.Service();
		while ((ENetConnection.EventType)ev[0].AsInt32() != ENetConnection.EventType.None)
		{
			var peer = ev[1].AsGodotObject() as ENetPacketPeer;
			switch ((ENetConnection.EventType)ev[0].AsInt32())
			{
				case ENetConnection.EventType.Connect:
					OnConnected(peer);
					break;
				case ENetConnection.EventType.Disconnect:
					OnDisconnected(peer);
					return;
				case ENetConnection.EventType.Receive:
					OnReceive(peer);
					break;
				case ENetConnection.EventType.Error:
					StatusChanged?.Invoke("Erro de transporte na conexao com o servidor");
					break;
			}
			ev = _connection.Service();
		}
	}

	private void OnConnected(ENetPacketPeer peer)
	{
		_serverPeer = peer;
		// Primeiro passo do protocolo: o server so encaminha submissoes e aceita
		// notas depois que este peer manda PROFESSOR_HELLO.
		ProfessorHelloPkt.Create().Send(peer);
		StatusChanged?.Invoke("Conectado ao servidor. Registrado como professor.");
		ConnectedToServer?.Invoke();
		RequestRooms();
	}

	private void OnDisconnected(ENetPacketPeer _peer)
	{
		bool wasConnected = IsConnectedToServer();
		_serverPeer = null;
		if (_connection != null)
		{
			_connection.Destroy();
			_connection = null;
		}
		StatusChanged?.Invoke("Conexao com o servidor encerrada");
		if (wasConnected)
		{
			DisconnectedFromServer?.Invoke();
		}
	}

	private void OnReceive(ENetPacketPeer peer)
	{
		byte[] data = peer.GetPacket();
		if (data.Length == 0)
		{
			return;
		}
		switch ((PacketTypeClass.PACKET_TYPE)data[0])
		{
			case PacketTypeClass.PACKET_TYPE.PEER_ID:
				PeerId peerId = PeerId.CreateFromData(data);
				PeerIdReceived?.Invoke(peerId.Id);
				break;
			case PacketTypeClass.PACKET_TYPE.REFRESH:
				RoomsReceived?.Invoke(RefreshClass.CreateFromData(data));
				break;
			case PacketTypeClass.PACKET_TYPE.MINIGAME_SUBMISSION:
				SubmissionReceived?.Invoke(MinigameSubmissionPkt.CreateFromData(data));
				break;
			// PEER_ID/REFRESH ja tratados; JOIN_ROOM/HAS_* sao fluxo de aluno.
		}
	}

	public override void _ExitTree()
	{
		DisconnectFromServer();
	}
}
