class_name RoomStorage

var host_ip: String
var port: int
var host_peer: ENetPacketPeer
var current_players: Array[ENetPacketPeer]
var current_players_id: Array[int]
var current_players_names: Array[String]

func _init(_ip: String, _port: int, _peer: ENetPacketPeer):
	host_ip = _ip
	port = _port
	host_peer = _peer

func add_player(player_peer: ENetPacketPeer) -> void:
	current_players.append(player_peer)

func remove_player(player_peer: ENetPacketPeer) -> void:
	current_players.erase(player_peer)

func add_player_id(player_id: int) -> void:
	current_players_id.append(player_id)

func remove_player_id(player_id: int) -> void:
	current_players_id.erase(player_id)

func add_player_name(name: String) -> void:
	current_players_names.append(name)

func remove_player_name(name: String) -> void:
	current_players_names.erase(name)
