using System;
using System.Collections.Generic;
using Godot;

public partial class Gun : Node3D
{
    [Export]
    public PackedScene BulletScene;

    [Export]
    public float BaseFireRate = 0.15f;

    [Export]
    public float BaseBulletSpeed = 70.0f;

    [Export]
    public float BaseBulletDamage = 25.0f;

    [Export]
    public int MaxActiveBullets = 30;

    private float _currentFireRate;
    private float _currentBulletSpeed;
    private float _currentBulletDamage;

    private Marker3D _muzzle;
    private Timer _fireCooldownTimer;
    private bool _canShoot = true;

    private AudioManager _audioManager;
    private const string SHOOT_SFX_PATH = "res://Assets/Audio/hit_1.wav";

    private Dictionary<PowerupType, Timer> _powerupTimers = new();

    public override void _Ready()
    {
        _muzzle = GetNode<Marker3D>("Muzzle");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        _currentFireRate = BaseFireRate;
        _currentBulletSpeed = BaseBulletSpeed;
        _currentBulletDamage = BaseBulletDamage;

        _fireCooldownTimer = new Timer();
        _fireCooldownTimer.WaitTime = _currentFireRate;
        _fireCooldownTimer.OneShot = true;
        _fireCooldownTimer.Timeout += OnFireCooldownTimeout;
        AddChild(_fireCooldownTimer);

        CreatePowerupTimer(PowerupType.FireRate);
        CreatePowerupTimer(PowerupType.BulletDamage);
        CreatePowerupTimer(PowerupType.BulletSpeed);
    }

    private void CreatePowerupTimer(PowerupType type)
    {
        var timer = new Timer();
        timer.Name = $"{type}PowerupTimer";
        timer.OneShot = true;
        timer.Timeout += () => RevertPowerup(type);
        AddChild(timer);
        _powerupTimers[type] = timer;
    }

    public void Shoot()
    {
        if (!_canShoot)
            return;

        if (GetTree().GetNodesInGroup("bullets").Count >= MaxActiveBullets)
            return;

        Node bulletInstanceNode = BulletScene.Instantiate();

        if (bulletInstanceNode is Bullet bullet)
        {
            GetTree().Root.AddChild(bullet);
            bullet.GlobalTransform = _muzzle.GlobalTransform;
            bullet.Speed = _currentBulletSpeed;
            bullet.Damage = _currentBulletDamage;
            bullet.InitializeVelocity(bullet.GlobalTransform.Basis);

            _canShoot = false;
            _fireCooldownTimer.WaitTime = _currentFireRate;
            _fireCooldownTimer.Start();

            _audioManager.PlaySFX(SHOOT_SFX_PATH);
        }
        else
        {
            GD.PrintErr(
                $"Gun ({Name}) Error: Instantiated scene {BulletScene?.ResourcePath ?? "NULL"} is not a Bullet"
            );
            bulletInstanceNode?.QueueFree();
        }
    }

    private void OnFireCooldownTimeout()
    {
        _canShoot = true;
    }

    public void ApplyPowerup(PowerupType type, float multiplier, float duration)
    {
        if (_powerupTimers.TryGetValue(type, out var existingTimer))
        {
            existingTimer.Stop();
            RevertPowerup(type);
        }

        switch (type)
        {
            case PowerupType.FireRate:
                _currentFireRate = BaseFireRate / multiplier;
                _fireCooldownTimer.WaitTime = _currentFireRate;
                GD.Print($"Gun: Fire Rate Powerup Applied New Rate Delay: {_currentFireRate}");
                break;
            case PowerupType.BulletDamage:
                _currentBulletDamage = BaseBulletDamage * multiplier;
                GD.Print($"Gun: Damage Powerup Applied New Damage: {_currentBulletDamage}");
                break;
            case PowerupType.BulletSpeed:
                _currentBulletSpeed = BaseBulletSpeed * multiplier;
                GD.Print($"Gun: Speed Powerup Applied New Speed: {_currentBulletSpeed}");
                break;
        }

        if (_powerupTimers.TryGetValue(type, out var timer))
        {
            timer.WaitTime = duration;
            timer.Start();
        }
    }

    private void RevertPowerup(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.FireRate:
                _currentFireRate = BaseFireRate;
                _fireCooldownTimer.WaitTime = _currentFireRate;
                GD.Print($"Gun: Fire Rate Powerup Expired Rate Delay: {_currentFireRate}");
                break;
            case PowerupType.BulletDamage:
                _currentBulletDamage = BaseBulletDamage;
                GD.Print($"Gun: Damage Powerup Expired Damage: {_currentBulletDamage}");
                break;
            case PowerupType.BulletSpeed:
                _currentBulletSpeed = BaseBulletSpeed;
                GD.Print($"Gun: Speed Powerup Expired Speed: {_currentBulletSpeed}");
                break;
        }
    }
}
