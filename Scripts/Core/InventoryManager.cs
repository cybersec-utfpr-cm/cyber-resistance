using Godot;
using System.Collections.Generic;

public partial class InventoryManager : Node
{
	public static InventoryManager Instance { get; private set; }

	private Dictionary<string, int> _items = new();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public void AddItem(string itemName, int amount)
	{
		if (_items.ContainsKey(itemName))
			_items[itemName] += amount;
		else
			_items[itemName] = amount;

		GD.Print($"InventoryManager: Recebeu {amount}x {itemName}. Agora tem {_items[itemName]}");
		SaveManager.Instance?.SaveGame();
	}

	public int GetItemCount(string itemName)
	{
		return _items.ContainsKey(itemName) ? _items[itemName] : 0;
	}

	public bool RemoveItem(string itemName, int amount)
	{
		if (!_items.ContainsKey(itemName) || _items[itemName] < amount)
			return false;

		_items[itemName] -= amount;
		if (_items[itemName] == 0)
			_items.Remove(itemName);
		SaveManager.Instance?.SaveGame();
		return true;
	}
private int _experience = 0;
private int _credits = 0;

[Signal] public delegate void InventoryChangedEventHandler();

public void AddExperience(int amount)
{
	if (amount <= 0) return;

	_experience += amount;
	GD.Print($"InventoryManager: recebeu {amount} XP. Total: {_experience} XP.");
	EmitSignal(SignalName.InventoryChanged);
	SaveManager.Instance?.SaveGame();
}

public int GetExperience()
{
	return _experience;
}

public void AddCredits(int amount)
{
	if (amount <= 0) return;

	_credits += amount;
	GD.Print($"InventoryManager: recebeu {amount} créditos. Total: {_credits} créditos.");
	EmitSignal(SignalName.InventoryChanged);
	SaveManager.Instance?.SaveGame();
}

public int GetCredits()
{
	return _credits;
}
	public Dictionary<string, int> GetItemsSnapshot()
	{
		return new Dictionary<string, int>(_items);
	}

	public void RestoreState(
		Dictionary<string, int> items,
		int experience,
		int credits
	)
	{
		_items.Clear();

		if (items != null)
		{
			foreach (var item in items)
			{
				if (!string.IsNullOrWhiteSpace(item.Key) && item.Value > 0)
					_items[item.Key] = item.Value;
			}
		}

		_experience = System.Math.Max(0, experience);
		_credits = System.Math.Max(0, credits);
	}
}
