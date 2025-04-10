using System;
using Godot;

public partial class Alien : GravityEntity
{
	[Signal]
	public delegate void DiedEventHandler();

	[ExportGroup("Stats")]
	[Export]
	public float Health = 100.0f;

	[Export]
	public float AttackDamage = 10.0f;

	[Export]
	public float AttackCooldown = 1.0f;

	[ExportGroup("AI Behavior")]
	[Export]
	public float MoveForce = 50.0f;

	[Export]
	public float TargetUpdateInterval = 0.5f;

	[Export]
	public float AttackRange = 3.0f;

	private Node3D _currentTarget = null;
	private float _targetUpdateTimer = 0.0f;
	private float _attackTimer = 0.0f;

	private Player _playerCache = null;
	private Ship _shipCache = null;

	private AudioManager _audioManager;
	private const string HIT_SFX_PATH = "res://Assets/Audio/hit_2.wav";

	public override void _Ready()
	{
		base._Ready();
		_audioManager = GetNode<AudioManager>("/root/AudioManager");
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		float fDelta = (float)delta;
		_targetUpdateTimer -= fDelta;
		_attackTimer -= fDelta;

		if (_targetUpdateTimer <= 0f)
		{
			FindClosestTarget();
			_targetUpdateTimer = TargetUpdateInterval;
		}

		if (_currentTarget != null && IsInstanceValid(_currentTarget))
		{
			MoveTowardsTarget(fDelta);
			CheckAttackRange();
		}
	}

	private void FindClosestTarget()
	{
		if (_playerCache == null || !IsInstanceValid(_playerCache))
			_playerCache = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);

		if (_shipCache == null || !IsInstanceValid(_shipCache))
			_shipCache = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);

		Node3D closestTarget = null;
		float closestDistSq = float.MaxValue;
		Vector3 currentPos = GlobalPosition;

		if (_playerCache != null && IsInstanceValid(_playerCache) && !_playerCache.IsDead())
		{
			float distSq = currentPos.DistanceSquaredTo(_playerCache.GlobalPosition);
			if (distSq < closestDistSq)
			{
				closestDistSq = distSq;
				closestTarget = _playerCache;
			}
		}

		if (_shipCache != null && IsInstanceValid(_shipCache) && !_shipCache.IsDead())
		{
			float distSq = currentPos.DistanceSquaredTo(_shipCache.GlobalPosition);
			if (distSq < closestDistSq)
			{
				if (closestTarget == null || distSq < closestDistSq)
				{
					closestTarget = _shipCache;
					closestDistSq = distSq;
				}
			}
		}
		_currentTarget = closestTarget;
	}

	private void MoveTowardsTarget(float delta)
	{
		Vector3 directionToTarget = (_currentTarget.GlobalPosition - GlobalPosition).Normalized();
		ApplyCentralForce(directionToTarget * MoveForce);
	}

	private void CheckAttackRange()
	{
		if (_attackTimer > 0f)
			return;

		float distSq = GlobalPosition.DistanceSquaredTo(_currentTarget.GlobalPosition);
		if (distSq < AttackRange * AttackRange)
		{
			Attack(_currentTarget);
			_attackTimer = AttackCooldown;
		}
	}

	private void Attack(Node3D target)
	{
		if (target is Player player)
		{
			player.TakeDamage(AttackDamage);
		}
		else if (target is Ship ship)
		{
			ship.TakeDamage(AttackDamage);
		}
	}

	private T GetNodeFromGroupHelper<T>(string group)
		where T : Node
	{
		var nodes = GetTree().GetNodesInGroup(group);
		if (nodes.Count > 0 && nodes[0] is T typedNode)
			return typedNode;
		return null;
	}

	public void TakeDamage(float amount)
	{
		if (Health <= 0)
			return;

		Health -= amount;
		_audioManager.PlaySFX(HIT_SFX_PATH);

		if (Health <= 0)
		{
			Die();
		}
	}

	private void Die()
	{
		if (Health <= 0 && !IsQueuedForDeletion())
		{
			EmitSignal(SignalName.Died);
			QueueFree();
		}
	}

	public bool IsDead() => Health <= 0;
}
