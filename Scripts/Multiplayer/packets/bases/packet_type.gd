class_name PacketTypeClass

enum PACKET_TYPE {
	PEER_ID = 0,
	ROOM_REQUEST = 5,
	START_ROOM = 10,
	JOIN_REQUEST = 15,
	JOIN_ROOM = 20,
	HAS_JOINED = 21,
	HAS_QUITTED = 22,
	QUIT_ROOM_REQUEST = 25,
	QUIT_ROOM = 30,
	REFRESH = 40,
	REFRESH_REQUEST = 50,
	ROOM_INFO = 60,
	MINIGAME_SUBMISSION = 70,
	PROFESSOR_HELLO = 80,
	MINIGAME_GRADE = 90
}

var packet_type: PACKET_TYPE
var flag: int

func _init(packet_type_: PacketTypeClass.PACKET_TYPE = PACKET_TYPE.PEER_ID, flag_: int = ENetPacketPeer.FLAG_RELIABLE) -> void:
	self.packet_type = packet_type_
	self.flag = flag_

func encode() -> PackedByteArray:
	var data: PackedByteArray
	data.resize(1)
	data.encode_u8(0, packet_type)
	return data

func decode(data: PackedByteArray) -> void:
	self.packet_type = data.decode_u8(0) as PACKET_TYPE

func send(target: ENetPacketPeer) -> void:
	target.send(0, encode(), flag)

func broadcast(server: ENetConnection) -> void:
	server.broadcast(0, encode(), flag)
