using System;
using Godot;

public partial class Alien : GravityEntity
{
    [Signal]
    public delegate void DiedEventHandler();

    [ExportGroup("Stats")] [Export] public float Health = 100.0f;

    [Export] public float AttackDamage = 10.0f;

    [Export] public float AttackCooldown = 1.0f;

    [ExportGroup("AI Behavior")] [Export] public float MoveForce = 10.0f;

    [Export] public float TargetUpdateInterval = 0.5f;

    [Export] public float AttackRange = 3.0f;

    [Export] public float MinSpeedToRotate = 0.1f;

    [Export] public float RotationSharpness = 10.0f;

    private Node3D _currentTarget;
    private float _targetUpdateTimer;
    private float _attackTimer;

    private Player _playerCache;
    private Ship _shipCache;

    private AudioManager _audioManager;
    private const string HitSfxPath = "res://Assets/Audio/hit_2.wav";

    private Vector3 CurrentVelocity => LinearVelocity;

    public override void _Ready()
    {
        base._Ready();
        _audioManager = GetNode<AudioManager>("/root/AudioManager");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        var fDelta = (float)delta;
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

        HandleRotation(fDelta);
    }

    private void HandleRotation(float delta)
    {
        var velocity = CurrentVelocity;
        var velocityOnPlane = new Vector3(velocity.X, 0, velocity.Z);

        if (!(velocityOnPlane.LengthSquared() > MinSpeedToRotate * MinSpeedToRotate)) return;
        var lookDirection = -velocityOnPlane.Normalized();
        var targetTransform = Transform.LookingAt(GlobalPosition + lookDirection, Vector3.Up);

        Basis = Basis.Slerp(targetTransform.Basis, delta * RotationSharpness);
    }


    private void FindClosestTarget()
    {
        if (_playerCache == null || !IsInstanceValid(_playerCache))
            _playerCache = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);

        if (_shipCache == null || !IsInstanceValid(_shipCache))
            _shipCache = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);

        Node3D closestTarget = null;
        var closestDistSq = float.MaxValue;
        var currentPos = GlobalPosition;

        if (_playerCache != null && IsInstanceValid(_playerCache) && !_playerCache.IsDead())
        {
            var distSq = currentPos.DistanceSquaredTo(_playerCache.GlobalPosition);
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestTarget = _playerCache;
            }
        }

        if (_shipCache != null && IsInstanceValid(_shipCache) && !_shipCache.IsDead())
        {
            var distSq = currentPos.DistanceSquaredTo(_shipCache.GlobalPosition);
            if (distSq < closestDistSq)
            {
                closestTarget = _shipCache;
                closestDistSq = distSq;
            }
        }

        _currentTarget = closestTarget;
    }

    private void MoveTowardsTarget(float delta)
    {
        if (_currentTarget == null || !IsInstanceValid(_currentTarget)) return;

        var directionToTarget = (GlobalPosition - _currentTarget.GlobalPosition).Normalized();
        ApplyCentralForce(-directionToTarget * MoveForce);
    }

    private void CheckAttackRange()
    {
        if (_attackTimer > 0f || _currentTarget == null || !IsInstanceValid(_currentTarget))
            return;

        var distSq = GlobalPosition.DistanceSquaredTo(_currentTarget.GlobalPosition);
        if (!(distSq < AttackRange * AttackRange)) return;
        Attack(_currentTarget);
        _attackTimer = AttackCooldown;
    }

    private void Attack(Node3D target)
    {
        switch (target)
        {
            case Player player:
                player.TakeDamage(AttackDamage);
                break;
            case Ship ship:
                ship.TakeDamage(AttackDamage);
                break;
        }
    }

    private T GetNodeFromGroupHelper<T>(string group)
        where T : Node
    {
        var nodes = GetTree().GetNodesInGroup(group);
        if (nodes.Count <= 0 || nodes[0] is not T typedNode) return null;
        return IsInstanceValid(typedNode) ? typedNode : null;
    }


    public void TakeDamage(float amount)
    {
        if (Health <= 0)
            return;

        Health -= amount;
        _audioManager?.PlaySFX(HitSfxPath);

        if (Health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (!(Health <= 0) || IsQueuedForDeletion()) return;
        EmitSignal(SignalName.Died);
        QueueFree();
    }

    public bool IsDead() => Health <= 0;
}