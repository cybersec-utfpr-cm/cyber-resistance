extends Area2D

# No Inspetor, você arrasta o arquivo .tscn para cá
@export_file("*.tscn") var target_scene: String

func _on_body_entered(body: Node) -> void:
	# Player está nos groups "player" (singular, do .tscn) e "players" (plural,
	# adicionado em runtime). Aqui filtramos por "player"; client_packet_handler
	# usa "players" pra despawn. Renomear qualquer um quebra um dos lados.
	if body.is_in_group("player"):
		if not body.get("is_authority"):
			return
		if target_scene == "":
			push_warning("Porta sem destino definido!")
			return
			
		teleport()

func teleport() -> void:
	GameManager.goto_scene(target_scene)
