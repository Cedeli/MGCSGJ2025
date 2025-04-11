using System;
using Godot;

public partial class Player : GravityEntity, IInputReceiver
{
	private AudioManager _audioManager;
	public const string PlayerGroup = "player";

	[Signal]
	public delegate void DiedEventHandler();

	[Signal]
	public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

	private const float MinPitchAngle = -Mathf.Pi / 2.0f + 0.01f;
	private const float MaxPitchAngle = Mathf.Pi / 2.0f - 0.01f;

	[ExportGroup("Components")]
	[Export]
	private Node3D _cameraPivot;

	[Export]
	private Camera3D _camera;

	[Export]
	private Gun _gun;

	[ExportGroup("Input")]
	[Export(PropertyHint.Range, "0.01,1.0")]
	public float MouseSensitivity = 0.25f;

	[Export]
	public bool InvertY;

	[ExportGroup("Stats")]
	[Export]
	public float MaxHealth = 100f;
	private float _currentHealth;

	private MovementController _currentMovementController;
	private PlanetaryMovementController _planetaryMovement;
	private InputManager _inputManager;
	private InputBuffer _inputBuffer;
	private Vector2 _movementInput;
	private float _yawAngle;

	public float CurrentHealth => _currentHealth;

	public override void _Ready()
	{
		base._Ready();

		if (_cameraPivot == null)
			GD.PrintErr($"{Name}: Camera Pivot not assigned.");
		if (_camera == null)
			GD.PrintErr($"{Name}: Camera3D not assigned.");

		Input.SetMouseMode(Input.MouseModeEnum.Captured);
		_currentHealth = MaxHealth;
		_planetaryMovement = new PlanetaryMovementController(this);
		_currentMovementController = _planetaryMovement;

		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		AddToGroup(PlayerGroup);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		var fDelta = (float)delta;
		UpdateCameraOrientation();
		_currentMovementController?.PhysicsProcess(fDelta, _movementInput);
		ProcessBufferedJump();
	}

	public void OnMoveInput(Vector2 direction) => _movementInput = direction.LimitLength();

	public void OnLookInput(Vector2 lookDelta)
	{
		_yawAngle -= Mathf.DegToRad(lookDelta.X * MouseSensitivity);
		_yawAngle = Mathf.Wrap(_yawAngle, -Mathf.Pi, Mathf.Pi);
		if (_camera != null)
			UpdateCameraPitch(lookDelta.Y);
	}

	public void OnShootInput()
	{
		if (_gun != null)
		{
			_gun.Shoot();
		}
		else
		{
			GD.PrintErr($"{Name}: Cannot shoot, Gun reference is null.");
		}
	}

	public void OnReloadInput()
	{
		if (_gun != null)
		{
			_gun.Reload();
		}
		else
		{
			GD.PrintErr($"{Name}: Cannot reload, Gun reference is null.");
		}
	}

	public void SetInputBuffer(InputBuffer buffer) => _inputBuffer = buffer;

	public void SetInputManager(InputManager manager) => _inputManager = manager;

	private void UpdateCameraOrientation()
	{
		if (_cameraPivot == null || GetGravityDirection() == Vector3.Zero)
			return;
		Vector3 targetUp = -GetGravityDirection().Normalized();
		if (targetUp == Vector3.Zero)
			targetUp = Vector3.Up;
		Vector3 currentForwardHint = -GlobalTransform.Basis.Z.Normalized();
		if (currentForwardHint == Vector3.Zero)
			currentForwardHint = Vector3.Forward;
		Basis planetAlignedBasis = CreateBasisFromUp(targetUp, currentForwardHint);
		Basis yawRotation = Basis.Identity.Rotated(targetUp, _yawAngle);
		Basis finalPivotBasis = (yawRotation * planetAlignedBasis).Orthonormalized();
		_cameraPivot.GlobalTransform = new Transform3D(finalPivotBasis, GlobalPosition);
	}

	private void UpdateCameraPitch(float lookYDelta)
	{
		if (_camera == null)
			return;
		float pitchChange = Mathf.DegToRad(-lookYDelta * MouseSensitivity * (InvertY ? -1f : 1f));
		_camera.RotateX(pitchChange);
		Vector3 cameraRotation = _camera.Rotation;
		cameraRotation.X = Mathf.Clamp(cameraRotation.X, MinPitchAngle, MaxPitchAngle);
		_camera.Rotation = cameraRotation;
	}

	private void ProcessBufferedJump()
	{
		if (!(_inputBuffer?.HasBufferedAction("jump") ?? false))
			return;
		if (_currentMovementController?.TryJump() ?? false)
			_inputBuffer.ConsumeBufferedAction("jump");
	}

	public Node3D GetCameraPivot() => _cameraPivot;

	public Gun GetGun() => _gun;

	public void TakeDamage(float amount)
	{
		if (_currentHealth <= 0)
			return;
		_currentHealth = Mathf.Max(0, _currentHealth - amount);
		_audioManager = GetNode<AudioManager>("/root/AudioManager");
		_audioManager?.PlaySFX("res://Assets/Audio/hit_1.wav");
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		if (_currentHealth <= 0)
			Die();
	}

	private void Die()
	{
		if (ProcessMode != ProcessModeEnum.Disabled)
		{
			GD.Print("Player Died");
			EmitSignal(SignalName.Died);
			ProcessMode = ProcessModeEnum.Disabled;
			SetCollisionLayerValue(1, false);
			Visible = false;
			Input.SetMouseMode(Input.MouseModeEnum.Visible);
		}
	}

	public bool IsDead() => _currentHealth <= 0;

	public void Heal(float amount)
	{
		if (_currentHealth <= 0)
			return;
		var oldHealth = _currentHealth;
		_currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
		if (_currentHealth > oldHealth)
		{
			EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		}
	}

	public bool TryAddGunReserveAmmo(int amount)
	{
		if (_gun != null)
		{
			return _gun.AddReserveAmmo(amount);
		}
		GD.PrintErr($"{Name}: Cannot add reserve ammo, Gun reference is null.");
		return false;
	}

	public void ApplyGunPowerup(PowerupType type, float multiplier, float duration)
	{
		if (_gun != null)
		{
			_gun.ApplyPowerup(type, multiplier, duration);
		}
		else
		{
			GD.PrintErr($"{Name}: Cannot apply powerup, Gun reference is null.");
		}
	}
}
