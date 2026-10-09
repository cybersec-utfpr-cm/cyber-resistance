extends Node

# Armazena o caminho da cena atual (ex: "res://maps/floresta.tscn")
var current_scene_path: String = ""

func _ready() -> void:
	# Define a cena inicial assim que o jogo começa
	current_scene_path = get_tree().current_scene.scene_file_path

# Ponto ÚNICO de troca de cena durante o jogo. Chamar
# get_tree().change_scene_to_file direto não dispara SCENE_SYNC e os outros
# clientes ficam sem saber em que cena este player está.
func goto_scene(path: String) -> void:
	call_deferred("_deferred_goto_scene", path)

# Door exports são salvos pelo editor como "uid://..."; mas scene_file_path
# devolve "res://...". Sem normalizar, players_scenes mistura os dois e o
# filtro de spawn no PlayerSpawner falha.
func _resolve_scene_path(path: String) -> String:
	if not path.begins_with("uid://"):
		return path
	var id: int = ResourceUID.text_to_id(path)
	if id == ResourceUID.INVALID_ID or not ResourceUID.has_id(id):
		return path
	return ResourceUID.get_id_path(id)

func _deferred_goto_scene(path: String) -> void:
	var resolved: String = _resolve_scene_path(path)
	ClientPacketHandler.players_scenes[ClientPacketHandler.my_id] = resolved
	current_scene_path = resolved
	get_tree().change_scene_to_file(resolved)
	if GamePacketHandler.is_host:
		SceneSyncPacket.create(ClientPacketHandler.my_id, resolved).broadcast(GamePacketHandler.host_connection)
	else:
		SceneSyncPacket.create(ClientPacketHandler.my_id, resolved).send(GamePacketHandler.host_peer)
