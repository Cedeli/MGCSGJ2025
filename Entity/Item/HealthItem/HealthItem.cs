// Entity/Item/HealthItem.cs
using System;
using Godot;

public partial class HealthItem : Item
{
	[Export]
	public float HealthToRestore = 25.0f;

	protected override bool ApplyEffect(Player player)
	{
		if (player == null)
			return false;

		GD.Print($"Applying {HealthToRestore} health to {player.Name}");
		player.Heal(HealthToRestore);
		return true;
	}

	public override void _Ready()
	{
		base._Ready();
	}
}
