class_name ProfessorHelloPkt extends PacketTypeClass

# Cliente do tipo "professor" -> server central.
# Primeira mensagem que a instância do professor envia ao conectar.
# Sinaliza ao server que este peer deve receber submissões e enviar notas
# (em vez de tratar como um aluno comum). Sem payload por enquanto.

static func create() -> ProfessorHelloPkt:
	return ProfessorHelloPkt.new(PACKET_TYPE.PROFESSOR_HELLO, ENetPacketPeer.FLAG_RELIABLE)

static func create_from_data(data: PackedByteArray) -> ProfessorHelloPkt:
	var new_packet: ProfessorHelloPkt = ProfessorHelloPkt.new(PACKET_TYPE.PROFESSOR_HELLO, ENetPacketPeer.FLAG_RELIABLE)
	new_packet.decode(data)
	return new_packet
