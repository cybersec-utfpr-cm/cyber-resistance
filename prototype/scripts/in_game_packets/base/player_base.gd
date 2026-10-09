class_name InGameTypeClass

enum PACKET_TYPE {
	PLAYER_PACKET = 0,
	TEXT_PACKET = 10,
	SCENE_SYNC_PACKET = 11,
	SCENE_FORCE_PACKET = 12,
	MINIGAME_ASSIGN = 20,
	MINIGAME_ANSWER = 21,
	MINIGAME_PROGRESS = 22,
	MINIGAME_GRADE_RESULT = 23
}

var packet_type: PACKET_TYPE
var flag: int

func _init(packet_type_: InGameTypeClass.PACKET_TYPE = PACKET_TYPE.PLAYER_PACKET, flag_: int = ENetPacketPeer.FLAG_RELIABLE) -> void:
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
