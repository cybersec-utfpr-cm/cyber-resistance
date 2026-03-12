using Godot;
using System.Collections.Generic;

public partial class LocationManager : Node {
	private Dictionary<string, Marker2D> _locations = new();

	public override void _Ready() {
		var spawnPoints = GetParent().GetNodeOrNull<Node>("SpawnPoints");
		
		if(spawnPoints == null) {
			GD.PrintErr("LocationManager.cs: SpawnPoints não encontrado!");
			return;
		}
		foreach (Node child in spawnPoints.GetChildren()) {
			if(child is Marker2D marker){
				_locations[marker.Name] = marker;
				GD.Print("LocationManager.cs: Registrado location: " + marker.Name);
			}
		}
	}

	//public void RegisterLocations() {
		//_locations.Clear();
//
		//var world = GetParent();
//
		//if (world == null) {
			//GD.PrintErr("World não encontrado");
			//return;
		//}
//
		//var spawnPoints = world.GetNodeOrNull<Node2D>("SpawnPoints");
//
		//if (spawnPoints == null) {
			//GD.PrintErr("SpawnPoints NÃO encontrado na cena: ", world.Name);
			//return;
		//}
//
		//foreach (Node child in spawnPoints.GetChildren()) {
			//if (child is Marker2D marker) {
				//_locations[marker.Name] = marker;
//
				//GD.Print("Location registrada: ", marker.Name);
			//}
		//}
	//}

	public Vector2 GetLocation(string name) {
		if (_locations.ContainsKey(name))
			return _locations[name].GlobalPosition;

		GD.PrintErr("Location não encontrada: ", name);

		return Vector2.Zero;
	}
}
