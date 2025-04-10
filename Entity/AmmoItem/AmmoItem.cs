using System;
using Godot;

public partial class AmmoItem : Item
{
	[Export]
	public int AmmoToAdd = 10; // todo

	protected override bool ApplyEffect(Player player)
	{
		if (player == null)
			return false;

		GD.Print($"Adding {AmmoToAdd} ammo to {player.Name}");
		// player.AddAmmo(AmmoToAdd); todo
		return true;
	}

	public override void _Ready()
	{
		base._Ready();
	}
}
