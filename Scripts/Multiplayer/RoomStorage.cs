using Godot;
using System.Collections.Generic;

public class RoomStorage
{
	public string HostIp;
	public int Port;
	public ENetPacketPeer HostPeer;
	public List<ENetPacketPeer> CurrentPlayers = new();
	public List<int> CurrentPlayersId = new();
	public List<string> CurrentPlayersNames = new();

	public RoomStorage(string ip, int port, ENetPacketPeer peer)
	{
		HostIp = ip;
		Port = port;
		HostPeer = peer;
	}

	public void AddPlayer(ENetPacketPeer playerPeer) => CurrentPlayers.Add(playerPeer);

	public void RemovePlayer(ENetPacketPeer playerPeer) => CurrentPlayers.Remove(playerPeer);

	public void AddPlayerId(int playerId) => CurrentPlayersId.Add(playerId);

	public void RemovePlayerId(int playerId) => CurrentPlayersId.Remove(playerId);

	public void AddPlayerName(string name) => CurrentPlayersNames.Add(name);

	public void RemovePlayerName(string name) => CurrentPlayersNames.Remove(name);
}
