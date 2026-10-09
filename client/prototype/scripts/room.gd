extends PanelContainer
class_name RoomItem

@export_file("*.tscn") var room_scene_path: String = "res://prototype/scenes/legacy/chat.tscn"
var id: int = -1

func _init() -> void:
	ClientPacketHandler.join_room.connect(join_room)

#Send a join request packet to the server
func _on_join_button_down() -> void:
	#print(("Room"),id, ClientPacketHandler.client_id)
	JoinRequestClass.create(id, ClientPacketHandler.my_id, ClientPacketHandler.temporary_player_name).send(ProtNetworkHandler.server_peer)
	#print("(Room) ID: ", id)

func setup_room(summary: RoomSummary) -> void:
	id = summary.id
	$HBoxContainer/VBoxContainer/Label.text = "%d/4 jogadores" % summary.player_count
	$HBoxContainer/Players.text = "\n".join(summary.player_names)

func join_room(room_id: int) -> void:
	if room_id == id:
		auto_join()

func auto_join() -> void:
	get_tree().change_scene_to_file(room_scene_path)
