using Godot;
using System;
using System.Collections.Generic;

public partial class Settings : Control
{
	private void _on_back_icon_bt_pressed() {
		GetParent<Screens>().ShowScreen("Desktop");
	}
	private void _on_wifi_button_pressed() {
		GetParent<Screens>().ShowScreen("WiFi");
	}
}
