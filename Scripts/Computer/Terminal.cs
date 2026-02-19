using Godot;
using System;
using System.Collections.Generic;

public partial class Terminal : Control
{
	private void _on_back_icon_bt_pressed() {
		GetParent<Screens>().ShowScreen("Desktop");
	}
}
