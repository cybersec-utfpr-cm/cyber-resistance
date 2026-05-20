using Godot;
using System;
using System.Collections.Generic;

public partial class Desktop : Control
{
	private void _on_terminal_icon_bt_pressed() {
		GetParent<Screens>().ShowScreen("Terminal");
	}
	
	private void _on_settings_icon_bt_pressed() {
		GetParent<Screens>().ShowScreen("Settings");
	}
	
	private void _on_wifi_button_pressed() {
		GetParent<Screens>().ShowScreen("Settings"); 
	}
}
