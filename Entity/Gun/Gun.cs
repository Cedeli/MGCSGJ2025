using System;
using System.Collections.Generic;
using Godot;

public partial class Gun : Node3D
{
	[Signal]
	public delegate void AmmoChangedEventHandler(int currentClip, int currentReserve);

	[ExportGroup("Gun Properties")]
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

	[Export]
	public float ReloadTime = 1.5f;

	[ExportGroup("Ammo")]
	[Export]
	public int ClipSize = 30;

	[Export]
	public int MaxReserveAmmo = 120;

	[Export]
	public int InitialReserveAmmo = 60;

	private float _currentFireRate;
	private float _currentBulletSpeed;
	private float _currentBulletDamage;
	private int _currentClipAmmo;
	private int _currentReserveAmmo;

	private Marker3D _muzzle;
	private Timer _fireCooldownTimer;
	private Timer _reloadTimer;
	private bool _canShoot = true;
	private bool _isReloading = false;

	private AudioManager _audioManager;
	private const string SHOOT_SFX_PATH = "res://Assets/Audio/bullet_1.wav";
	private const string RELOAD_SFX_PATH = "res://Assets/Audio/reload_placeholder.wav";

	private Dictionary<PowerupType, Timer> _powerupTimers = new();

	public override void _Ready()
	{
		_muzzle = GetNode<Marker3D>("Muzzle");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		_currentFireRate = BaseFireRate;
		_currentBulletSpeed = BaseBulletSpeed;
		_currentBulletDamage = BaseBulletDamage;

		_currentClipAmmo = ClipSize;
		_currentReserveAmmo = InitialReserveAmmo;

		_fireCooldownTimer = new Timer { Name = "FireCooldownTimer", OneShot = true };
		_fireCooldownTimer.Timeout += OnFireCooldownTimeout;
		AddChild(_fireCooldownTimer);

		_reloadTimer = new Timer
		{
			Name = "ReloadTimer",
			WaitTime = ReloadTime,
			OneShot = true,
		};
		_reloadTimer.Timeout += OnReloadFinished;
		AddChild(_reloadTimer);

		CreatePowerupTimer(PowerupType.FireRate);
		CreatePowerupTimer(PowerupType.BulletDamage);
		CreatePowerupTimer(PowerupType.BulletSpeed);

		EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo);
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
		if (!_canShoot || _isReloading)
			return;

		if (_currentClipAmmo <= 0)
		{
			Reload();
			return;
		}

		if (GetTree().GetNodesInGroup("bullets").Count >= MaxActiveBullets)
		{
			GD.Print("Max bullets reached, cannot shoot.");
			return;
		}

		if (BulletScene == null)
		{
			GD.PrintErr($"Gun ({Name}) Error: Cannot shoot, BulletScene is null.");
			return;
		}

		Node bulletInstanceNode = BulletScene.Instantiate();
		if (bulletInstanceNode is Bullet bullet)
		{
			GetTree().Root.AddChild(bullet);
			bullet.GlobalTransform = _muzzle.GlobalTransform;
			bullet.Speed = _currentBulletSpeed;
			bullet.Damage = _currentBulletDamage;
			bullet.InitializeVelocity(bullet.GlobalTransform.Basis);

			_currentClipAmmo--;
			EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo);

			_canShoot = false;
			_fireCooldownTimer.WaitTime = _currentFireRate;
			_fireCooldownTimer.Start();

			_audioManager.PlaySFX(SHOOT_SFX_PATH);
		}
		else
		{
			bulletInstanceNode?.QueueFree();
		}
	}

	public void Reload()
	{
		if (_isReloading || _currentReserveAmmo <= 0 || _currentClipAmmo == ClipSize)
		{
			return;
		}

		_isReloading = true;
		_canShoot = false;
		_reloadTimer.Start();
		GD.Print("Gun: Reloading...");
		// _audioManager.PlaySFX(RELOAD_SFX_PATH);
	}

	private void OnReloadFinished()
	{
		int ammoNeeded = ClipSize - _currentClipAmmo;
		int ammoToTransfer = Math.Min(ammoNeeded, _currentReserveAmmo);

		if (ammoToTransfer > 0)
		{
			_currentClipAmmo += ammoToTransfer;
			_currentReserveAmmo -= ammoToTransfer;
			EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo); // Update HUD
		}

		_isReloading = false;
		_canShoot = true;
		GD.Print("Gun: reload finished");
	}

	private void OnFireCooldownTimeout()
	{
		_canShoot = true;
	}

	public void ApplyPowerup(PowerupType type, float multiplier, float duration)
	{
		if (_powerupTimers.TryGetValue(type, out var existingTimer) && !existingTimer.IsStopped())
		{
			existingTimer.Stop();
			RevertPowerup(type);
		}

		switch (type)
		{
			case PowerupType.FireRate:
				_currentFireRate = BaseFireRate / multiplier;
				_fireCooldownTimer.WaitTime = _currentFireRate;
				GD.Print($"Gun: new rate delay: {_currentFireRate}s");
				break;
			case PowerupType.BulletDamage:
				_currentBulletDamage = BaseBulletDamage * multiplier;
				GD.Print($"Gun: new damage: {_currentBulletDamage}");
				break;
			case PowerupType.BulletSpeed:
				_currentBulletSpeed = BaseBulletSpeed * multiplier;
				GD.Print($"Gun: new speed: {_currentBulletSpeed}");
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
		if (_powerupTimers.TryGetValue(type, out var timer) && timer.IsStopped())
		{
			switch (type)
			{
				case PowerupType.FireRate:
					if (Math.Abs(_currentFireRate - (BaseFireRate)) > 0.001)
					{
						_currentFireRate = BaseFireRate;
						_fireCooldownTimer.WaitTime = _currentFireRate;
					}
					break;
				case PowerupType.BulletDamage:
					if (Math.Abs(_currentBulletDamage - BaseBulletDamage) > 0.001)
					{
						_currentBulletDamage = BaseBulletDamage;
					}
					break;
				case PowerupType.BulletSpeed:
					if (Math.Abs(_currentBulletSpeed - BaseBulletSpeed) > 0.001)
					{
						_currentBulletSpeed = BaseBulletSpeed;
					}
					break;
			}
		}
		else { }
	}

	public bool AddReserveAmmo(int amount)
	{
		if (_currentReserveAmmo >= MaxReserveAmmo)
		{
			return false;
		}

		_currentReserveAmmo = Math.Min(_currentReserveAmmo + amount, MaxReserveAmmo);
		EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo);
		GD.Print($"Gun: added {amount} reserve ammo {_currentReserveAmmo}");
		return true;
	}
}
