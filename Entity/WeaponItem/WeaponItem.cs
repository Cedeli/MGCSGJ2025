using System;
using Godot;

public partial class WeaponItem : Item
{
	[Export]
	public PackedScene WeaponScene; // assign weapon scene, maybe random probability

	protected override bool ApplyEffect(Player player)
	{
		if (player == null || WeaponScene == null)
			return false;
		GD.Print($"Giving weapon {WeaponScene.ResourcePath} to {player.Name}");
		// bool equipped = player.EquipWeapon(WeaponScene);  todo
		return true; //equipped;
	}

	public override void _Ready()
	{
		base._Ready();
	}
}
