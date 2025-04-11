using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Gun : Node3D
{
	[Signal]
	public delegate void AmmoChangedEventHandler(int currentClip, int currentReserve);

	[ExportGroup("Gun Properties")] [Export]
	public PackedScene BulletScene;

	[Export] public float BaseFireRate = 0.15f;

	[Export] public float BaseBulletSpeed = 70.0f;

	[Export] public float BaseBulletDamage = 25.0f;

	[Export] public int MaxActiveBullets = 30;

	[Export] public float ReloadTime = 1.5f;

	[ExportGroup("Ammo")] [Export] public int ClipSize = 30;

	[Export] public int MaxReserveAmmo = 120;

	[Export] public int InitialReserveAmmo = 60;

	private float _currentFireRate;
	private float _currentBulletSpeed;
	private float _currentBulletDamage;
	private int _currentClipAmmo;
	private int _currentReserveAmmo;

	private Marker3D _muzzle;
	private Timer _fireCooldownTimer;
	private Timer _reloadTimer;
	private bool _canShoot = true;
	private bool _isReloading;

	private AudioManager _audioManager;

	private const string ShootSfxPath = "res://Assets/Audio/bullet_1.wav";
	private const string OutSfxPath = "res://Assets/Audio/gun_out.wav";
	private const string ReloadSfxPath = "res://Assets/Audio/gun_reload.ogg";

	private readonly Dictionary<PowerupType, Timer> _powerupTimers = new();

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

	private bool IsAnyPowerupActive()
	{
		return _powerupTimers.Values.Any(timer => !timer.IsStopped());
	}

	public void Shoot()
	{
		if (!_canShoot || _isReloading)
		{
			// if (_currentClipAmmo <= 0 && !IsAnyPowerupActive())
			// {
			//     _audioManager.PlaySFX(OUT_SFX_PATH);
			// }
			return;
		}

		if (_currentClipAmmo <= 0 && !IsAnyPowerupActive())
		{
			// out SFX too long
			// _audioManager.PlaySFX(OutSfxPath);
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

		var bulletInstanceNode = BulletScene.Instantiate();
		if (bulletInstanceNode is not Bullet bullet) return;
		GetTree().Root.AddChild(bullet);
		bullet.GlobalTransform = _muzzle.GlobalTransform;
		bullet.Speed = _currentBulletSpeed;
		bullet.Damage = _currentBulletDamage;
		bullet.InitializeVelocity(bullet.GlobalTransform.Basis);

		var powerupActive = IsAnyPowerupActive();
		if (!powerupActive)
		{
			_currentClipAmmo--;
			EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo);
		}

		_canShoot = false;
		_fireCooldownTimer.WaitTime = _currentFireRate;
		_fireCooldownTimer.Start();

		_audioManager.PlaySFX(ShootSfxPath);
	}

	public void Reload()
	{
		if (IsAnyPowerupActive() || _isReloading || _currentReserveAmmo <= 0 || _currentClipAmmo == ClipSize)
		{
			return;
		}

		_isReloading = true;
		_canShoot = false;
		_reloadTimer.Start();
		_audioManager.PlaySFX(ReloadSfxPath);
	}
	
	private void OnReloadFinished()
	{
		var ammoNeeded = ClipSize - _currentClipAmmo;
		var ammoToTransfer = Math.Min(ammoNeeded, _currentReserveAmmo);

		if (ammoToTransfer > 0)
		{
			_currentClipAmmo += ammoToTransfer;
			_currentReserveAmmo -= ammoToTransfer;
			EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo);
		}

		_isReloading = false;
		_canShoot = true;
	}

	private void OnFireCooldownTimeout()
	{
		_canShoot = true;
	}

	public void ApplyPowerup(PowerupType type, float multiplier, float duration)
	{
		if (_powerupTimers.TryGetValue(type, out var existingTimer) && !existingTimer.IsStopped())
		{
			GD.Print($"Gun: Reapplying powerup {type}, resetting timer.");
			existingTimer.Stop();
			RevertPowerupStats(type);
		}

		GD.Print($"Gun: Applying powerup {type} (Multiplier: {multiplier}, Duration: {duration}s)");

		switch (type)
		{
			case PowerupType.FireRate:
				_currentFireRate = BaseFireRate / multiplier;
				_fireCooldownTimer.WaitTime = _currentFireRate;
				GD.Print($"Gun: New fire rate delay: {_currentFireRate}s");
				break;
			case PowerupType.BulletDamage:
				_currentBulletDamage = BaseBulletDamage * multiplier;
				GD.Print($"Gun: New bullet damage: {_currentBulletDamage}");
				break;
			case PowerupType.BulletSpeed:
				_currentBulletSpeed = BaseBulletSpeed * multiplier;
				GD.Print($"Gun: New bullet speed: {_currentBulletSpeed}");
				break;
			default:
				GD.PushWarning($"Gun: ApplyPowerup called with unhandled type: {type}");
				return;
		}

		if (_powerupTimers.TryGetValue(type, out var timer))
		{
			timer.WaitTime = duration;
			timer.Start();
		}

		if (!_isReloading) return;
		_reloadTimer.Stop();
		_isReloading = false;
		_canShoot = true;
	}

	private void RevertPowerupStats(PowerupType type)
	{
		if (!_powerupTimers.TryGetValue(type, out var timer) || !timer.IsStopped()) return;
		GD.Print($"Gun: Reverting stats for powerup {type}");
		switch (type)
		{
			case PowerupType.FireRate:
				if (Math.Abs(_currentFireRate - BaseFireRate) > 0.001f)
				{
					_currentFireRate = BaseFireRate;
					if (_fireCooldownTimer.IsStopped() ||
						Math.Abs(_fireCooldownTimer.WaitTime - _currentFireRate) > 0.001f)
					{
						_fireCooldownTimer.WaitTime = _currentFireRate;
					}

					GD.Print($"Gun: Fire rate reverted to base: {_currentFireRate}s");
				}

				break;
			case PowerupType.BulletDamage:
				if (Math.Abs(_currentBulletDamage - BaseBulletDamage) > 0.001f)
				{
					_currentBulletDamage = BaseBulletDamage;
					GD.Print($"Gun: Bullet damage reverted to base: {_currentBulletDamage}");
				}

				break;
			case PowerupType.BulletSpeed:
				if (Math.Abs(_currentBulletSpeed - BaseBulletSpeed) > 0.001f)
				{
					_currentBulletSpeed = BaseBulletSpeed;
					GD.Print($"Gun: Bullet speed reverted to base: {_currentBulletSpeed}");
				}

				break;
			default:
				GD.PushWarning($"Gun: RevertPowerupStats called with unhandled type: {type}");
				break;
		}
	}

	private void RevertPowerup(PowerupType type)
	{
		RevertPowerupStats(type);

		if (!IsAnyPowerupActive() && _currentClipAmmo <= 0)
		{
			Reload();
		}
	}


	public bool AddReserveAmmo(int amount)
	{
		if (amount <= 0) return false;
		if (_currentReserveAmmo >= MaxReserveAmmo) return false;

		var previousReserve = _currentReserveAmmo;
		_currentReserveAmmo = Math.Min(_currentReserveAmmo + amount, MaxReserveAmmo);
		var addedAmount = _currentReserveAmmo - previousReserve;

		EmitSignal(SignalName.AmmoChanged, _currentClipAmmo, _currentReserveAmmo);
		GD.Print($"Gun: Added {addedAmount} reserve ammo. Total: {_currentReserveAmmo}/{MaxReserveAmmo}");
		return true;
	}
}
