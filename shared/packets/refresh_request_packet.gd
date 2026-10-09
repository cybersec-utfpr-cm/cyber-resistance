class_name RefreshRequestClass extends PacketTypeClass

static func create() -> RefreshRequestClass:
	var refresh_req: RefreshRequestClass = RefreshRequestClass.new(PacketTypeClass.PACKET_TYPE.REFRESH_REQUEST, ENetPacketPeer.FLAG_RELIABLE)
	return refresh_req

static func create_from_data(data: PackedByteArray) -> RefreshRequestClass:
	var new_refresh_req: RefreshRequestClass = RefreshRequestClass.new(PacketTypeClass.PACKET_TYPE.REFRESH_REQUEST, ENetPacketPeer.FLAG_RELIABLE)
	new_refresh_req.decode(data)
	return new_refresh_req
