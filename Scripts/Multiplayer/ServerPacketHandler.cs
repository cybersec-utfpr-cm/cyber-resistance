using Godot;
using System.Collections.Generic;

/// Roteador de pacotes do servidor central (autoload "ServerPacketHandler").
/// Port direto de server_packet_handler.gd.
public partial class ServerPacketHandler : Node
{
	public static ServerPacketHandler Instance { get; private set; }

	// range(255,-1,-1) + pop_back() devolve room_ids em ordem crescente (0,1,2...).
	// Quando uma sala fecha, o id volta pra num_room e fica disponivel pra reuso.
	public List<int> NumRoom = new();
	public List<int> CreatedRoomsId = new();
	public Dictionary<int, RoomStorage> Rooms = new();

	// Respostas do minigame por sala, recebidas do host quando as 2 duplas
	// concluem. Consumido pela UI do professor (passo futuro).
	public class MinigameSubmissionEntry
	{
		public Godot.Collections.Array<Godot.Collections.Dictionary> Teams;
		public long ReceivedAt;
	}

	public Dictionary<int, MinigameSubmissionEntry> MinigameSubmissions = new();

	// Peer do professor (so um por enquanto). Se null, ninguem esta logado como
	// professor; submissoes ficam armazenadas em MinigameSubmissions e sao
	// despachadas assim que um PROFESSOR_HELLO chegar.
	public ENetPacketPeer ProfessorPeer = null;

	public override void _Ready()
	{
		Instance = this;
		ServerNetworkHandler.Instance.FromClientPacket += HandleClientPacket;
		var refreshTimer = new Timer
		{
			WaitTime = 5.0,
			Autostart = true
		};
		refreshTimer.Timeout += OnRefreshTick;
		AddChild(refreshTimer);
		for (int i = 255; i >= 0; i--)
		{
			NumRoom.Add(i);
		}
	}

	// Push periodico de refresh pra quem está no lobby (não está em sala nem é o
	// professor). Players in-game não precisam — a lista mudaria sob eles.
	private void OnRefreshTick()
	{
		foreach (ENetPacketPeer peer in ServerNetworkHandler.Instance.PeersConnected.Values)
		{
			if (!peer.GetMeta("in_room", false).AsBool())
			{
				SendRefresh(peer);
			}
		}
	}

	private void HandleClientPacket(ENetPacketPeer peer, byte[] data)
	{
		var packetType = (PacketTypeClass.PACKET_TYPE)data[0];
		switch (packetType)
		{
			case PacketTypeClass.PACKET_TYPE.ROOM_REQUEST:
				RoomRequest(peer, data);
				break;
			case PacketTypeClass.PACKET_TYPE.JOIN_REQUEST:
				JoinRequest(peer, data);
				break;
			case PacketTypeClass.PACKET_TYPE.QUIT_ROOM_REQUEST:
				QuitRoomRequest(peer, data);
				break;
			case PacketTypeClass.PACKET_TYPE.REFRESH_REQUEST:
				SendRefresh(peer);
				break;
			case PacketTypeClass.PACKET_TYPE.ROOM_INFO:
				SaveRoomInfo(peer, data);
				break;
			case PacketTypeClass.PACKET_TYPE.MINIGAME_SUBMISSION:
				SaveMinigameSubmission(peer, data);
				break;
			case PacketTypeClass.PACKET_TYPE.PROFESSOR_HELLO:
				RegisterProfessor(peer);
				break;
			case PacketTypeClass.PACKET_TYPE.MINIGAME_GRADE:
				RouteGrade(peer, data);
				break;
		}
	}

	public void SaveRoomInfo(ENetPacketPeer peer, byte[] data)
	{
		RoomInfoClass roomPacket = RoomInfoClass.CreateFromData(data);
		int roomId = roomPacket.RoomId;
		var newRoom = new RoomStorage(roomPacket.HostIp, roomPacket.RoomPort, peer);
		newRoom.AddPlayer(peer);
		newRoom.AddPlayerId(roomPacket.PlayerId);
		newRoom.AddPlayerName(roomPacket.PlayerName);
		peer.SetMeta("name", roomPacket.PlayerName);
		CreatedRoomsId.Add(roomId);
		Rooms[roomId] = newRoom;
		peer.SetMeta("in_room", true);
		GD.Print("(Server handler) info saved: ", Rooms);
	}

	public void SaveMinigameSubmission(ENetPacketPeer peer, byte[] data)
	{
		MinigameSubmissionPkt packet = MinigameSubmissionPkt.CreateFromData(data);
		int roomId = packet.RoomId;
		// So aceita do host da sala (impede client malicioso de injetar).
		if (!Rooms.ContainsKey(roomId) || Rooms[roomId].HostPeer != peer)
		{
			GD.Print($"(Server handler) submission ignorada — peer não é host da sala {roomId}");
			return;
		}
		MinigameSubmissions[roomId] = new MinigameSubmissionEntry
		{
			Teams = packet.Teams,
			ReceivedAt = (long)Time.GetTicksMsec()
		};
		GD.Print($"(Server handler) minigame submission room={roomId}");
		// Encaminha pro professor (se conectado). Reaproveita o mesmo pacote.
		if (ProfessorPeer != null)
		{
			if (ProfessorPeer.GetState() == ENetPacketPeer.PeerState.Connected)
			{
				MinigameSubmissionPkt.Create(roomId, packet.Teams).Send(ProfessorPeer);
			}
			else
			{
				ProfessorPeer = null;
			}
		}
	}

	public void RouteGrade(ENetPacketPeer peer, byte[] data)
	{
		// So aceita do peer registrado como professor.
		if (peer != ProfessorPeer)
		{
			GD.Print("(Server handler) MINIGAME_GRADE ignorado - peer nao e o professor");
			return;
		}
		MinigameGradePkt pkt = MinigameGradePkt.CreateFromData(data);
		if (!Rooms.ContainsKey(pkt.RoomId))
		{
			GD.Print($"(Server handler) MINIGAME_GRADE para sala inexistente {pkt.RoomId}");
			return;
		}
		ENetPacketPeer hostPeer = Rooms[pkt.RoomId].HostPeer;
		if (hostPeer.GetState() != ENetPacketPeer.PeerState.Connected)
		{
			GD.Print($"(Server handler) host da sala {pkt.RoomId} nao esta conectado");
			return;
		}
		// Encaminha o pacote intacto ao host - ele decodifica e traduz em
		// MinigameGradeResultPkt (in-game) para a dupla.
		hostPeer.Send(0, data, (int)ENetPacketPeer.FlagReliable);
		GD.Print($"(Server handler) MINIGAME_GRADE encaminhado para host da sala {pkt.RoomId}");
	}

	public void RegisterProfessor(ENetPacketPeer peer)
	{
		ProfessorPeer = peer;
		peer.SetMeta("is_professor", true);
		GD.Print("(Server handler) professor conectado");
		// Despacha submissoes ja recebidas para que o prof comece a corrigir.
		foreach (KeyValuePair<int, MinigameSubmissionEntry> pair in MinigameSubmissions)
		{
			MinigameSubmissionPkt.Create(pair.Key, pair.Value.Teams).Send(peer);
		}
	}

	public void SendRefresh(ENetPacketPeer peer)
	{
		RefreshClass.Create(Rooms, CreatedRoomsId).Send(peer);
	}

	public void JoinRequest(ENetPacketPeer peer, byte[] data)
	{
		JoinRequestClass request = JoinRequestClass.CreateFromData(data);
		int room = request.Room;
		if (!CreatedRoomsId.Contains(room))
		{
			GD.Print("This room does not exist anymore, please refresh the page!");
			return;
		}
		if (Rooms[room].CurrentPlayers.Count >= 4)
		{
			GD.Print("The room is full!");
			return;
		}
		Rooms[room].AddPlayer(peer);
		Rooms[room].AddPlayerId(request.PlayerId);
		Rooms[room].AddPlayerName(request.PlayerName);
		peer.SetMeta("name", request.PlayerName);
		peer.SetMeta("in_room", true);
		JoinRoomClass
			.Create(room, Rooms[room].Port, Rooms[room].HostIp, Rooms[room].CurrentPlayersId)
			.Send(peer);
		for (int i = 0; i < Rooms[room].CurrentPlayersId.Count; i++)
		{
			if (Rooms[room].CurrentPlayers[i] != peer)
			{
				HasJoinedPkt.Create(Rooms[room].CurrentPlayersId)
					.Send(Rooms[room].CurrentPlayers[i]);
			}
		}
		GD.Print("(Server handler) All players in the room: ", Rooms[room].CurrentPlayersId);
	}

	public void RoomRequest(ENetPacketPeer peer, byte[] data)
	{
		RoomRequestClass request = RoomRequestClass.CreateFromData(data);
		int requesterId = request.Id;
		if (NumRoom.Count == 0)
		{
			GD.Print("Error: maximum rooms limit exceded!");
			return;
		}
		int roomId = PopNumRoom();
		StartRoomClass.Create(roomId, requesterId).Send(peer);
	}

	public void QuitRoomRequest(ENetPacketPeer peer, byte[] data)
	{
		QuitRequestClass quitRequest = QuitRequestClass.CreateFromData(data);
		int roomReqId = quitRequest.RoomId;
		GD.Print($"(Server handle) quitting room: {quitRequest.RoomId}");
		if (!Rooms.ContainsKey(roomReqId))
		{
			return;
		}

		RoomStorage room = Rooms[roomReqId];
		if (peer == room.HostPeer)
		{
			foreach (ENetPacketPeer currentPeer in new List<ENetPacketPeer>(room.CurrentPlayers))
			{
				currentPeer.SetMeta("in_room", false);
				QuitRoomClass.Create().Send(currentPeer);
			}
			Rooms.Remove(roomReqId);
			CreatedRoomsId.Remove(roomReqId);
			NumRoom.Add(roomReqId);
			NumRoom.Sort();
			return;
		}

		room.RemovePlayer(peer);
		room.RemovePlayerId(peer.GetMeta("id").AsInt32());
		room.RemovePlayerName(peer.GetMeta("name", "").AsString());
		peer.SetMeta("in_room", false);
		QuitRoomClass.Create().Send(peer);
		foreach (ENetPacketPeer currentPeer in room.CurrentPlayers)
		{
			HasQuittedPkt.Create(room.CurrentPlayersId).Send(currentPeer);
		}
	}

	private int PopNumRoom()
	{
		int roomId = NumRoom[^1];
		NumRoom.RemoveAt(NumRoom.Count - 1);
		return roomId;
	}
}
