extends Control

@onready var ip_line: LineEdit = $VBox/IpRow/IpLine
@onready var port_line: LineEdit = $VBox/PortRow/PortLine
@onready var start_button: Button = $VBox/StartButton
@onready var status_label: Label = $VBox/StatusLabel

func _ready() -> void:
	start_button.pressed.connect(_on_start_pressed)
	ip_line.text = _guess_local_ipv4()

func _guess_local_ipv4() -> String:
	for ip in IP.get_local_addresses():
		if ip.count(".") == 3 and (ip.begins_with("192.168.") or ip.begins_with("10.") or ip.begins_with("172.")):
			return ip
	return ""

func _on_start_pressed() -> void:
	var ip: String = ip_line.text.strip_edges()
	if ip.is_empty():
		status_label.text = "Status: informe o IP"
		return
	var port_text: String = port_line.text.strip_edges()
	if not port_text.is_valid_int():
		status_label.text = "Status: porta invalida"
		return
	var port: int = int(port_text)
	if port < 1 or port > 65535:
		status_label.text = "Status: porta fora do intervalo (1-65535)"
		return
	start_button.disabled = true
	if ProtNetworkHandler.start_server(ip, port):
		status_label.text = "Status: rodando em %s:%d" % [ip, port]
		ip_line.editable = false
		port_line.editable = false
	else:
		status_label.text = "Status: falha ao iniciar (IP/porta invalidos?)"
		start_button.disabled = false
