extends Node

func _ready() -> void:
	for player_id in ClientPacketHandler.spawned_ids:
		ClientPacketHandler.spawn_player_signal.emit(player_id)
