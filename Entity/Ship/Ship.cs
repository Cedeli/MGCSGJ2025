using System;
using Godot;

public partial class Ship : GravityEntity
{
	// Services
	private AudioManager _audioManager;

	public const string ShipGroup = "ship";

	[Signal]
	public delegate void DiedEventHandler();

	[Signal]
	public delegate void HullChangedEventHandler(float currentHull, float maxHull);

	[ExportGroup("Ship Stats")]
	[Export]
	public float MaxHull = 200f;
	private float _currentHull;

	public float CurrentHull => _currentHull;

	public override void _Ready()
	{
		base._Ready();
		_currentHull = MaxHull;
		EmitSignal(SignalName.HullChanged, _currentHull, MaxHull);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
	}

	public void TakeDamage(float amount)
	{
		if (_currentHull <= 0)
			return;
		_currentHull = Mathf.Max(0, _currentHull - amount);
		_audioManager = GetNode<AudioManager>("/root/AudioManager");
		_audioManager.PlaySFX("res://Assets/Audio/hit_2.wav");
		EmitSignal(SignalName.HullChanged, _currentHull, MaxHull);
		if (_currentHull <= 0)
			Die();
	}

	private void Die()
	{
		if (ProcessMode != ProcessModeEnum.Disabled)
		{
			GD.Print("Ship Destroyed!");
			EmitSignal(SignalName.Died);
			ProcessMode = ProcessModeEnum.Disabled;
			SetCollisionLayerValue(1, false);
			Visible = false;
		}
	}

	public bool IsDead() => _currentHull <= 0;
}
