extends Control

const MULTIJOGADOR = preload("res://prototype/scenes/menu/client_main.tscn")
@onready var start: Button = $Panel/MarginContainer/HBoxContainer/VBoxContainer/Start
@onready var line_edit: LineEdit = $Panel/MarginContainer/HBoxContainer/VBoxContainer/LineEdit
@onready var exit: Button = $Panel/MarginContainer/HBoxContainer/VBoxContainer/Exit


var original_text: String = ""

func _ready() -> void:
	ClientNet.on_peer_connected.connect(load_multiplayer_scene)
	ClientNet.on_connection_error.connect(on_connection_error)
	original_text = start.text


func _on_start_pressed() -> void:
	ClientNet.start_client(line_edit.text, 42069)
	line_edit.editable = false
	start.disabled = true
	exit.disabled = true
	start.text = "loading..."

func _on_exit_button_down() -> void:
	get_tree().quit()

func on_connection_error() -> void:
	print("Erro ao tentar se conectar")
	line_edit.editable = true
	start.disabled = false
	exit.disabled = false
	start.text = original_text

func load_multiplayer_scene() -> void:
	get_tree().change_scene_to_file("res://prototype/scenes/menu/client_main.tscn")
