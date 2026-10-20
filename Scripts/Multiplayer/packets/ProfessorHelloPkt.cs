using Godot;

// Cliente do tipo "professor" -> server central.
// Primeira mensagem que a instancia do professor envia ao conectar.
// Sinaliza ao server que este peer deve receber submissoes e enviar notas
// (em vez de tratar como um aluno comum). Sem payload por enquanto.
public class ProfessorHelloPkt : PacketTypeClass
{
	public ProfessorHelloPkt(
		PACKET_TYPE packetType = PACKET_TYPE.PROFESSOR_HELLO,
		int flag = (int)ENetPacketPeer.FlagReliable
	) : base(packetType, flag)
	{
	}

	public static ProfessorHelloPkt Create()
	{
		return new ProfessorHelloPkt(PACKET_TYPE.PROFESSOR_HELLO, (int)ENetPacketPeer.FlagReliable);
	}

	public static ProfessorHelloPkt CreateFromData(byte[] data)
	{
		var newPacket = new ProfessorHelloPkt(
			PACKET_TYPE.PROFESSOR_HELLO,
			(int)ENetPacketPeer.FlagReliable
		);
		newPacket.Decode(data);
		return newPacket;
	}
}
