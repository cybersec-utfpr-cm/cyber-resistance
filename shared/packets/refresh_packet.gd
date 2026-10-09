class_name RefreshClass extends PacketTypeClass

var summaries: Array[RoomSummary]

static func create(rooms_data: Dictionary, rooms_id: Array[int]) -> RefreshClass:
	var refresh: RefreshClass = RefreshClass.new(PacketTypeClass.PACKET_TYPE.REFRESH, ENetPacketPeer.FLAG_RELIABLE)
	refresh.summaries = []
	for room_id in rooms_id:
		if not rooms_data.has(room_id):
			continue
		# Untyped: RoomStorage existe so no projeto server/.
		# Este create() so e chamado pelo server; client e professor usam create_from_data.
		var room = rooms_data[room_id]
		var summary: RoomSummary = RoomSummary.new()
		summary.id = room_id
		summary.player_count = room.current_players.size()
		summary.player_names = room.current_players_names.duplicate()
		refresh.summaries.append(summary)
	return refresh

static func create_from_data(data: PackedByteArray) -> RefreshClass:
	var refresh: RefreshClass = RefreshClass.new(PacketTypeClass.PACKET_TYPE.REFRESH, ENetPacketPeer.FLAG_RELIABLE)
	refresh.decode(data)
	return refresh

func encode() -> PackedByteArray:
	var buf: PackedByteArray = super.encode()
	buf.append(summaries.size())
	for summary in summaries:
		buf.append(summary.id)
		buf.append(summary.player_count)
		for name in summary.player_names:
			var name_bytes: PackedByteArray = name.to_utf8_buffer()
			buf.append(name_bytes.size())
			buf.append_array(name_bytes)
	return buf

func decode(data: PackedByteArray) -> void:
	super.decode(data)
	summaries = []
	var offset: int = 1
	var num_rooms: int = data.decode_u8(offset)
	offset += 1
	for _r in range(num_rooms):
		var summary: RoomSummary = RoomSummary.new()
		summary.id = data.decode_u8(offset)
		offset += 1
		summary.player_count = data.decode_u8(offset)
		offset += 1
		summary.player_names = []
		for _p in range(summary.player_count):
			var name_size: int = data.decode_u8(offset)
			offset += 1
			summary.player_names.append(data.slice(offset, offset + name_size).get_string_from_utf8())
			offset += name_size
		summaries.append(summary)
