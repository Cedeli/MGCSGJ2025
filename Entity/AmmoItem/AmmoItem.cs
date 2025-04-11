using System;
using Godot;

public partial class AmmoItem : Item
{
	[Export]
	public int AmmoToAdd = 10;

	protected override bool ApplyEffect(Player player)
	{
		if (player == null)
			return false;

		GD.Print($"Attempting to add {AmmoToAdd} reserve ammo to {player.Name}'s gun.");
		return player.TryAddGunReserveAmmo(AmmoToAdd);
	}
}
