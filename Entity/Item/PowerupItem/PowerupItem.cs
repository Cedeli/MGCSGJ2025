using System;
using Godot;

public enum PowerupType
{
	FireRate,
	BulletDamage,
	BulletSpeed,
}

public partial class PowerupItem : Item
{
	[Export]
	public PowerupType EffectType = PowerupType.FireRate;

	[Export(PropertyHint.Range, "0.1, 3.0, 0.1")]
	public float Multiplier = 1.5f;

	[Export(PropertyHint.Range, "1.0, 30.0, 1.0")]
	public float Duration = 10.0f;

	protected override bool ApplyEffect(Player player)
	{
		player?.ApplyGunPowerup(EffectType, Multiplier, Duration);
		return true;
	}

	public override void _Ready()
	{
		base._Ready();
	}
}
