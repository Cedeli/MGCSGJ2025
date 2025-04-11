using System;
using Godot;

public partial class Bullet : GravityEntity
{
	[Export] public float Speed = 70.0f;

	[Export] public float Lifetime = 3.0f;

	public float Damage = 25.0f;

	private Timer _lifetimeTimer;
	private bool _hitOccurred;

	public override void _Ready()
	{
		base._Ready();

		LinearDamp = 0;
		AngularDamp = 0;
		ContactMonitor = true;
		MaxContactsReported = 4;

		_lifetimeTimer = new Timer();
		_lifetimeTimer.WaitTime = Lifetime;
		_lifetimeTimer.OneShot = true;
		_lifetimeTimer.Timeout += OnLifetimeTimeout;
		AddChild(_lifetimeTimer);
		_lifetimeTimer.Start();

		AddToGroup("bullets");
	}

	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		base._IntegrateForces(state);

		if (_hitOccurred)
			return;

		var contactCount = state.GetContactCount();
		if (contactCount <= 0) return;
		for (var i = 0; i < contactCount; i++)
		{
			if (state.GetContactColliderObject(i) is not Node collider) continue;

			switch (collider)
			{
				case Alien alien when IsInstanceValid(alien) && !alien.IsQueuedForDeletion() && !alien.IsDead():
					alien.TakeDamage(Damage);
					_hitOccurred = true;
					QueueFree();
					return;
				case Player:
					continue;
				default:
					_hitOccurred = true;
					QueueFree();
					break;
			}
		}
	}

	public void InitializeVelocity(Basis initialBasis)
	{
		LinearVelocity = -initialBasis.Z.Normalized() * Speed;
	}

	private void OnLifetimeTimeout()
	{
		if (!_hitOccurred)
		{
			QueueFree();
		}
	}

	public override void _ExitTree()
	{
		RemoveFromGroup("bullets");
		base._ExitTree();
	}
}
