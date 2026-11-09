class_name MinigameAssignPkt extends InGameTypeClass

enum ROLE { DOC = 0, QUIZ = 1 }

var target_id: int
var team: int
var role: int
var partner_id: int
var member_ids: Array[int]
var activity_id: String = ""

static func create(_target_id: int, _team: int, _role: int, _partner_id: int, _member_ids: Array[int], _activity_id: String = "") -> MinigameAssignPkt:
	var new_packet: MinigameAssignPkt = MinigameAssignPkt.new(PACKET_TYPE.MINIGAME_ASSIGN, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.target_id = _target_id
	new_packet.team = _team
	new_packet.role = _role
	new_packet.partner_id = _partner_id
	new_packet.member_ids = _member_ids
	new_packet.activity_id = _activity_id
	return new_packet

static func create_from_data(data: PackedByteArray) -> MinigameAssignPkt:
	var new_packet: MinigameAssignPkt = MinigameAssignPkt.new(PACKET_TYPE.MINIGAME_ASSIGN, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet

func encode() -> PackedByteArray:
	var data: PackedByteArray = super.encode()
	data.append(target_id)
	data.append(team)
	data.append(role)
	data.append(partner_id)
	data.append(member_ids.size())
	for id in member_ids:
		data.append(id)
	var act_bytes: PackedByteArray = activity_id.to_utf8_buffer()
	data.append(act_bytes.size())
	data.append_array(act_bytes)
	return data

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	target_id = data.decode_u8(1)
	team = data.decode_u8(2)
	role = data.decode_u8(3)
	partner_id = data.decode_u8(4)
	var count: int = data.decode_u8(5)
	member_ids = []
	for i in count:
		member_ids.append(data.decode_u8(6 + i))
	var offset: int = 6 + count
	if data.size() > offset:
		var act_size: int = data.decode_u8(offset)
		if data.size() >= offset + 1 + act_size:
			activity_id = data.slice(offset + 1, offset + 1 + act_size).get_string_from_utf8()
