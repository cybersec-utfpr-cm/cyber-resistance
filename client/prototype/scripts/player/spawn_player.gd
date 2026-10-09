extends Node

const PLAYER = preload("uid://qynwjxu3m3a0")

func _init() -> void:
	ClientPacketHandler.spawn_player_signal.connect(player_spawner)
	ClientPacketHandler.player_scene_changed.connect(_on_player_scene_changed)

# spawn_player_signal pode ter sido emitido antes deste spawner entrar na árvore
# (a troca de cena é deferida). Ao carregar a cena, reprocessamos os ids conhecidos.
func _ready() -> void:
	for player_id in ClientPacketHandler.spawned_ids:
		player_spawner(player_id)

func _has_player(id: int) -> bool:
	for child in get_children():
		if child.get("owner_id") == id:
			return true
	return false

func player_spawner(id: int) -> void:
	if not is_inside_tree():
		return
	if _has_player(id):
		return
	var current_scene: String = get_tree().current_scene.scene_file_path
	if id != ClientPacketHandler.my_id and ClientPacketHandler.players_scenes.get(id, current_scene) != current_scene:
		return
	var player_instance: Node = PLAYER.instantiate()
	player_instance.owner_id = id
	add_child(player_instance)

func _on_player_scene_changed(player_id: int, scene_path: String) -> void:
	if not is_inside_tree():
		return
	var current_scene: String = get_tree().current_scene.scene_file_path
	if scene_path == current_scene:
		player_spawner(player_id)
	else:
		for child in get_children():
			if child.get("owner_id") == player_id:
				child.queue_free()
				return
