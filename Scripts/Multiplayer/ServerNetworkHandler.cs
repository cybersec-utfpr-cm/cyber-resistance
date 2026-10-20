using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// Camada de rede do servidor central (autoload "ProtNetworkHandler").
/// Port direto de network_handler.gd.
public partial class ServerNetworkHandler : Node
{
	public static ServerNetworkHandler Instance { get; private set; }

	/// Equivalente ao signal from_client_packet(peer, data).
	public event System.Action<ENetPacketPeer, byte[]> FromClientPacket;

	public ENetConnection ServerConnection;
	// range(255,-1,-1) + pop_back() devolve ids em ordem crescente (0,1,2,3...).
	// O cliente usa essa ordem para decidir papel: my_id % 4 == 0 vira host de sala.
	public List<int> AvailablePeerIds = new();
	public Dictionary<int, ENetPacketPeer> PeersConnected = new();

	// [TCC eval] log periodico de RTT/perda em user://enet_stats_server_<unix>.csv.
	private const float StatsIntervalS = 1.0f;
	private FileAccess _statsFile;
	private float _statsAccum;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		for (int i = 255; i >= 0; i--)
		{
			AvailablePeerIds.Add(i);
		}
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Process(double delta)
	{
		if (ServerConnection == null)
		{
			return;
		}
		HandleEvents();
		StatsTick((float)delta, PeersConnected.Values);
	}

	public void HandleEvents()
	{
		Godot.Collections.Array packetEvent = ServerConnection.Service();
		var eventType = (ENetConnection.EventType)packetEvent[0].AsInt32();
		while (eventType != ENetConnection.EventType.None)
		{
			var peerSender = packetEvent[1].AsGodotObject() as ENetPacketPeer;
			switch (eventType)
			{
				case ENetConnection.EventType.Error:
					GD.Print("Packet received error");
					break;
				case ENetConnection.EventType.Connect:
					PeerConnected(peerSender);
					// Debug timeout — mesmo valor de antes para testes longos.
					peerSender.SetTimeout(0, 10000, 10000);
					break;
				case ENetConnection.EventType.Disconnect:
					PeerDisconnected(peerSender);
					break;
				case ENetConnection.EventType.Receive:
					FromClientPacket?.Invoke(peerSender, peerSender.GetPacket());
					break;
			}
			packetEvent = ServerConnection.Service();
			eventType = (ENetConnection.EventType)packetEvent[0].AsInt32();
		}
	}

	public bool StartServer(string ipAddress, int port = 42069)
	{
		ServerConnection = new ENetConnection();
		Error error = ServerConnection.CreateHostBound(ipAddress, port, 32, 0, 0, 0);
		if (error != Error.Ok)
		{
			GD.PrintErr($"ERRO FATAL AO CRIAR HOST: {(int)error}");
			ServerConnection = null;
			return false;
		}
		GD.Print($"Server started at {ipAddress}:{port}");
		StatsOpen("server");
		return true;
	}

	public void PeerConnected(ENetPacketPeer peer)
	{
		int peerId = PopAvailableId();
		peer.SetMeta("id", peerId);
		PeersConnected[peerId] = peer;
		// PEER_ID deve sair ANTES do REFRESH: o cliente decide se vai ser host
		// (my_id % 4 == 0) dentro do callback de refresh, então my_id precisa
		// estar setado.
		PeerId.Create(peerId).Send(peer);
		ServerPacketHandler.Instance.SendRefresh(peer);
		GD.Print($"(Server network) Peer: {peerId} successfully connected");
	}

	public void PeerDisconnected(ENetPacketPeer peer)
	{
		int peerId = peer.GetMeta("id").AsInt32();
		AvailablePeerIds.Add(peerId);
		PeersConnected.Remove(peerId);
		GD.Print($"Peer: {peerId} successfully disconnected");
	}

	private int PopAvailableId()
	{
		int peerId = AvailablePeerIds[^1];
		AvailablePeerIds.RemoveAt(AvailablePeerIds.Count - 1);
		return peerId;
	}

	// [TCC eval] abre arquivo de estatisticas para o papel atual.
	// Caminho real em OS.get_user_data_dir(); o print mostra o absoluto.
	private void StatsOpen(string role)
	{
		string path = $"user://enet_stats_{role}_{(long)Time.GetUnixTimeFromSystem()}.csv";
		_statsFile = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (_statsFile == null)
		{
			GD.PushError($"[stats] open failed: {path}");
			return;
		}
		_statsFile.StoreLine("timestamp,peer,rtt_ms,last_rtt_ms,packet_loss_pct");
		GD.Print($"[stats] gravando em {ProjectSettings.GlobalizePath(path)}");
	}

	// [TCC eval] amostra a cada StatsIntervalS segundos.
	// PEER_PACKET_LOSS escala 0..65535 (=100%); normalizamos para porcentagem.
	private void StatsTick(float delta, IEnumerable<ENetPacketPeer> peers)
	{
		if (_statsFile == null)
		{
			return;
		}
		_statsAccum += delta;
		if (_statsAccum < StatsIntervalS)
		{
			return;
		}
		_statsAccum = 0.0f;
		long ts = (long)Time.GetUnixTimeFromSystem();
		foreach (ENetPacketPeer peer in peers)
		{
			if (peer == null)
			{
				continue;
			}
			if (peer.GetState() != ENetPacketPeer.PeerState.Connected)
			{
				continue;
			}
			string label = peer.HasMeta("id") ? peer.GetMeta("id").AsInt32().ToString() : "peer";
			double rtt = peer.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime);
			double lastRtt = peer.GetStatistic(ENetPacketPeer.PeerStatistic.LastRoundTripTime);
			double loss = peer.GetStatistic(ENetPacketPeer.PeerStatistic.PacketLoss);
			_statsFile.StoreLine(FormattableString.Invariant(
				$"{ts},{label},{rtt:F1},{lastRtt:F1},{(loss / 65535.0) * 100.0:F4}"
			));
		}
		_statsFile.Flush();
	}
}
