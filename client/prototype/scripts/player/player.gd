extends CharacterBody2D

@onready var animation: AnimatedSprite2D = $Animation
@onready var camera: Camera2D = get_node_or_null("Camera2D")

const SPEED: float = 300
const PLAYER_COLORS: Array[Color] = [
	Color(1.0, 0.6, 0.6),
	Color(0.6, 0.8, 1.0),
	Color(0.6, 1.0, 0.7),
	Color(1.0, 1.0, 0.6),
]
var movement_direction: Vector2
var current_anim: String

var network_tick_rate: float = 0.02
var time_since_last_packet: float = 0.0
var last_sent_position: Vector2
#var last_sent_animation: String

var owner_id: int
var is_host: bool
# Só a instância dona deste id lê input e emite movimento.
# As outras 3 instâncias são marionetes movidas via pacotes.
var is_authority: bool:
	get: return owner_id == ClientPacketHandler.my_id

func _ready() -> void:
	is_host = GamePacketHandler.is_host
	#print("(Player) is host? id?", is_host, ClientPacketHandler.my_id)
	add_to_group("players")
	setup_player()
	setup_camera()

func setup_camera() -> void:
	if camera == null:
		return

	camera.enabled = is_authority
	if is_authority:
		camera.make_current()

func setup_player() -> void:
	if not is_authority:
		collision_layer = 0
	if is_host:
		PlayerHostPacketHandler.player_movement_signal.connect(player_movement_packet_handler)
	else:
		PlayerHostPacketHandler.host_movement_signal.connect(host_movement_packet_handler)
		if is_authority:
			var scene_path: String = get_tree().current_scene.scene_file_path
			if not scene_path.is_empty() and GamePacketHandler.can_send_to_host():
				SceneSyncPacket.create(owner_id, scene_path).send(GamePacketHandler.host_peer)
	animation.modulate = PLAYER_COLORS[owner_id % PLAYER_COLORS.size()]

func _physics_process(_delta: float) -> void:
	if get_viewport().gui_get_focus_owner() is LineEdit:
		return
	if MinigameSession.is_active: return
	if !is_authority: return
	movement_direction = Input.get_vector("walk_left", "walk_right", "walk_up", "walk_down")
	velocity = movement_direction * SPEED
	
	move_and_slide()
	animate()
	
	time_since_last_packet += _delta
	if time_since_last_packet >= network_tick_rate:
		time_since_last_packet = 0
		#print(is_host)
		#print("(Player) is host? spawned ids size? ", is_host, ClientPacketHandler.spawned_ids.size() )
		# Host broadcasta direto (autoridade). Player só unicasta ao host,
		# que vai rebroadcastar pros demais (ver player_movement_packet_handler).
		if is_host && ClientPacketHandler.spawned_ids.size() > 1:
			PlayerDataPacket.create(owner_id, global_position, animation.animation).broadcast(GamePacketHandler.host_connection)
			#print("host: ", GamePacketHandler.host_peer)
			#print("(Player) Pacote enviado pelo host")
		elif GamePacketHandler.can_send_to_host():
			#print("player: ", GamePacketHandler.host_peer)
			#print("(Player) Pacote enviado pelo player")
			PlayerDataPacket.create(owner_id, global_position, animation.animation).send(GamePacketHandler.host_peer)

func animate() -> void:
	var direction_key = movement_direction.round()
	
	match direction_key:
		Vector2.UP:
			animation.play("walk_up")
		Vector2.DOWN:
			animation.play("walk_down")
		Vector2.LEFT:
			animation.play("walk_left")
		Vector2.RIGHT:
			animation.play("walk_right")
		Vector2.ZERO:
			animation.play("idle")

# Host recebe movimento de um player: aplica local E rebroadcasta. ENet não
# devolve broadcast ao remetente, então o autor original não recebe duplicata.
func player_movement_packet_handler(data: PackedByteArray) -> void:
	var packet: PlayerDataPacket = PlayerDataPacket.create_from_data(data)

	if packet.id != owner_id:
		return
	#print("Player pos: ", packet.position)
	global_position = packet.position
	animation.play(packet.animation_name)
	PlayerDataPacket.create(owner_id, packet.position, animation.animation).broadcast(GamePacketHandler.host_connection)

func host_movement_packet_handler(data: PackedByteArray) -> void:
	# Eu autoritativo ignoro a versão broadcast — minha posição já é a fonte da verdade.
	if is_authority: return
	var packet: PlayerDataPacket = PlayerDataPacket.create_from_data(data)
	if packet.id != owner_id:
		return
	global_position = packet.position
	animation.play(packet.animation_name)
