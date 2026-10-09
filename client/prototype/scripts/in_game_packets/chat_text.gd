class_name ChatTextClass extends InGameTypeClass

# 255 = mensagem fora do minigame; 0/1 = dupla A/B.
const TEAM_NONE: int = 255

var sender_team: int = TEAM_NONE
var sender_name: String
var text: String

static func create(_sender_name: String, _text: String, _sender_team: int = TEAM_NONE) -> ChatTextClass:
	var new_packet: ChatTextClass = ChatTextClass.new(PACKET_TYPE.TEXT_PACKET, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.sender_team = _sender_team
	new_packet.sender_name = _sender_name
	new_packet.text = _text
	return new_packet

static func create_from_data(data: PackedByteArray) -> ChatTextClass:
	var new_packet: ChatTextClass = ChatTextClass.new(PACKET_TYPE.TEXT_PACKET, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	data.append(sender_team)
	var sender_name_data: PackedByteArray = sender_name.to_utf8_buffer()
	var text_data: PackedByteArray = text.to_utf8_buffer()
	data.append(sender_name_data.size())
	data.append_array(sender_name_data)
	data.append_array(text_data)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	sender_team = data.decode_u8(1)
	var sender_name_size: int = data.decode_u8(2)
	sender_name = data.slice(3, 3 + sender_name_size).get_string_from_utf8()
	text = data.slice(3 + sender_name_size).get_string_from_utf8()
