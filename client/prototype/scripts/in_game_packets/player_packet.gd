class_name PlayerDataPacket extends InGameTypeClass

const HEADER_SIZE = 10

var id: int
var position: Vector2
var animation_name: String

static func create(_id: int, _position: Vector2, _animation_name: String) -> PlayerDataPacket:
	var new_packet = PlayerDataPacket.new(PACKET_TYPE.PLAYER_PACKET, ENetPacketPeer.FLAG_UNSEQUENCED)
	new_packet.id = _id
	new_packet.position = _position
	new_packet.animation_name = _animation_name
	return new_packet

static func create_from_data(data: PackedByteArray) -> PlayerDataPacket:
	var new_packet: PlayerDataPacket = PlayerDataPacket.new()
	new_packet.decode(data)
	return new_packet

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	id = data.decode_u8(1)
	position = Vector2(data.decode_float(2), data.decode_float(6))
	var string_data = data.slice(10)
	animation_name = string_data.get_string_from_utf8()
	return

func encode() -> PackedByteArray:
	var new_array: PackedByteArray = super.encode()
	var animation: PackedByteArray = animation_name.to_utf8_buffer()
	new_array.resize(HEADER_SIZE)
	new_array.encode_u8(1, id)
	new_array.encode_float(2, position.x)
	new_array.encode_float(6, position.y)
	new_array.append_array(animation)
	return new_array

func send(target: ENetPacketPeer) -> void:
	target.send(0, encode(), flag)

func broadcast(server: ENetConnection) -> void:
	server.broadcast(0, encode(), flag)
