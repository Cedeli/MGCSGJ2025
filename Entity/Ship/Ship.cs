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

	private const string HIT_SFX_PATH = "res://Assets/Audio/hit_2.wav";

	// private const string REPAIR_SFX_PATH = "res://Assets/Audio/repair_placeholder.wav";

	public float CurrentHull => _currentHull;

	public override void _Ready()
	{
		base._Ready();
		_currentHull = MaxHull;
		_audioManager = GetNode<AudioManager>("/root/AudioManager");
		EmitSignal(SignalName.HullChanged, _currentHull, MaxHull);
		AddToGroup(ShipGroup);
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
		_audioManager?.PlaySFX(HIT_SFX_PATH);
		EmitSignal(SignalName.HullChanged, _currentHull, MaxHull);
		if (_currentHull <= 0)
			Die();
	}

	public bool HealHull(float amount)
	{
		if (_currentHull <= 0 || _currentHull >= MaxHull)
			return false;

		float oldHull = _currentHull;
		_currentHull = Mathf.Min(_currentHull + amount, MaxHull);

		if (_currentHull > oldHull)
		{
			EmitSignal(SignalName.HullChanged, _currentHull, MaxHull);
			// _audioManager?.PlaySFX(REPAIR_SFX_PATH);
			GD.Print($"Ship hull repaired by {amount}. Current: {_currentHull}/{MaxHull}");
			return true;
		}
		return false;
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
