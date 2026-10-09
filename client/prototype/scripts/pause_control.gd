extends Control

func _ready() -> void:
	ClientPacketHandler.quit_room.connect(quit_room)
	visible = false
	get_tree().paused = false

func _input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		toggle_pause()

func toggle_pause() -> void:
	var is_paused = !get_tree().paused
	get_tree().paused = is_paused
	visible = is_paused

func _on_continue_button_down() -> void:
	toggle_pause()

func _on_quit_button_down() -> void:
	get_tree().paused = false
	QuitRequestClass.create(ClientPacketHandler.current_room_id).send(ProtNetworkHandler.server_peer)

func quit_room() -> void:
	get_tree().change_scene_to_file("res://prototype/scenes/menu/client_main.tscn")
