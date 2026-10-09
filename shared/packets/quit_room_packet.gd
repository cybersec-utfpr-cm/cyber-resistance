class_name QuitRoomClass extends PacketTypeClass

static func create() -> QuitRoomClass:
	var refresh_req: QuitRoomClass = QuitRoomClass.new(PacketTypeClass.PACKET_TYPE.QUIT_ROOM)
	return refresh_req

static func create_from_data(data: PackedByteArray) -> QuitRoomClass:
	var new_refresh_req: QuitRoomClass = QuitRoomClass.new(PacketTypeClass.PACKET_TYPE.QUIT_ROOM)
	new_refresh_req.decode(data)
	return new_refresh_req
