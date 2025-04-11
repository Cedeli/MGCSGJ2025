using System;
using Godot;

public partial class Player : GravityEntity, IInputReceiver
{
    private AudioManager _audioManager;
    private SettingsManager _settingsManager;
    public const string PlayerGroup = "player";

    [Signal]
    public delegate void DiedEventHandler();

    [Signal]
    public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

    private const float MinPitchAngle = -Mathf.Pi / 2.0f + 0.01f;
    private const float MaxPitchAngle = Mathf.Pi / 2.0f - 0.01f;

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

    private bool _weaponBobbingEnabled = true;
    private Vector3 _currentWeaponBobOffset = Vector3.Zero;
    private Vector3 _targetWeaponBobOffset = Vector3.Zero;
    private Vector3 _initialWeaponLocalPosition = Vector3.Zero;

    [ExportGroup("Weapon Bobbing Settings")]
    [Export]
    private float WeaponBobbingIntensity = 0.0015f;

    [Export]
    private float WeaponBobbingReturnSpeed = 8.0f;

    [Export]
    private float WeaponBobbingFollowSpeed = 12.0f;

    [Export]
    private float MaxWeaponBobOffsetX = 0.08f;

    [Export]
    private float MaxWeaponBobOffsetY = 0.05f;

    // --- Other Fields ---
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
        AddToGroup("input_receivers");
        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _settingsManager = GetNode<SettingsManager>("/root/SettingsManager");

        _currentHealth = MaxHealth;
        _planetaryMovement = new PlanetaryMovementController(this);
        _currentMovementController = _planetaryMovement;

        if (_gun != null)
        {
            _initialWeaponLocalPosition = _gun.Position;
        }
        else
        {
            GD.PrintErr($"Player ({Name}): Gun node is not assigned, cannot setup weapon bobbing.");
        }

        if (_settingsManager != null)
        {
            _weaponBobbingEnabled = _settingsManager.WeaponBobbingEnabled;
            if (
                !_settingsManager.IsConnected(
                    SettingsManager.SignalName.WeaponBobbingToggled,
                    Callable.From<bool>(OnWeaponBobbingToggled)
                )
            )
                _settingsManager.WeaponBobbingToggled += OnWeaponBobbingToggled;
        }
        else
        {
            GD.PrintErr(
                $"Player ({Name}): SettingsManager not found, weapon bobbing defaults to enabled."
            );
        }

        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
        AddToGroup(PlayerGroup);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (
            _settingsManager != null
            && _settingsManager.IsConnected(
                SettingsManager.SignalName.WeaponBobbingToggled,
                Callable.From<bool>(OnWeaponBobbingToggled)
            )
        )
        {
            _settingsManager.WeaponBobbingToggled -= OnWeaponBobbingToggled;
        }
    }

    public override void _Process(double delta)
    {
        if (_gun != null)
        {
            if (_weaponBobbingEnabled)
            {
                ProcessWeaponBobbing((float)delta);
            }
            else if (_gun.Position != _initialWeaponLocalPosition)
            {
                _targetWeaponBobOffset = Vector3.Zero;
                _currentWeaponBobOffset = _currentWeaponBobOffset.Lerp(
                    Vector3.Zero,
                    (float)delta * WeaponBobbingReturnSpeed * 2f
                );
                _gun.Position = _initialWeaponLocalPosition + _currentWeaponBobOffset;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        var fDelta = (float)delta;
        UpdateCameraOrientation();

        _currentMovementController?.PhysicsProcess(fDelta, _movementInput);

        ProcessBufferedJump();
    }

    // temporary raw input for bobbing outside of input api because of time constraints
    public override void _Input(InputEvent @event)
    {
        if (Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (@event is InputEventMouseMotion mouseMotion)
            {
                Vector2 lookDeltaRaw = mouseMotion.Relative;
                UpdateCameraPitch(lookDeltaRaw.Y);
                _yawAngle -= Mathf.DegToRad(lookDeltaRaw.X * MouseSensitivity);
                _yawAngle = Mathf.Wrap(_yawAngle, -Mathf.Pi, Mathf.Pi);
                if (_weaponBobbingEnabled && _gun != null)
                {
                    _targetWeaponBobOffset.X -= lookDeltaRaw.X * WeaponBobbingIntensity;
                    _targetWeaponBobOffset.Y += lookDeltaRaw.Y * WeaponBobbingIntensity;

                    _targetWeaponBobOffset.X = Mathf.Clamp(
                        _targetWeaponBobOffset.X,
                        -MaxWeaponBobOffsetX,
                        MaxWeaponBobOffsetX
                    );
                    _targetWeaponBobOffset.Y = Mathf.Clamp(
                        _targetWeaponBobOffset.Y,
                        -MaxWeaponBobOffsetY,
                        MaxWeaponBobOffsetY
                    );
                    _targetWeaponBobOffset.Z = 0;
                }
            }
        }
    }

    public void ProcessMoveInput(Vector2 direction)
    {
        _movementInput = direction.LimitLength();
    }

    public void OnMoveInput(Vector2 direction) => ProcessMoveInput(direction);

    public void OnLookInput(Vector2 lookDelta) { }

    public void OnShootInput()
    {
        if (_gun != null)
        {
            _gun.Shoot();
        }
    }

    public void OnReloadInput()
    {
        if (_gun != null)
        {
            _gun.Reload();
        }
    }

    public void SetInputBuffer(InputBuffer buffer)
    {
        _inputBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public void SetInputManager(InputManager manager)
    {
        _inputManager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    private void UpdateCameraOrientation()
    {
        if (_cameraPivot == null)
            return;

        var targetUp = -GetGravityDirection().Normalized();

        var right = Vector3.Right;
        right = (right - targetUp * right.Dot(targetUp)).Normalized();

        if (right.LengthSquared() < 0.01f)
        {
            right = (Vector3.Forward - targetUp * Vector3.Forward.Dot(targetUp)).Normalized();
            if (right.LengthSquared() < 0.01f)
            {
                right = (Vector3.Up - targetUp * Vector3.Up.Dot(targetUp)).Normalized();
            }
        }

        var forward = targetUp.Cross(right).Normalized();
        right = forward.Cross(targetUp).Normalized();

        var initialBasis = new Basis(right, targetUp, forward);

        var yawBasis = new Basis(targetUp, -_yawAngle);
        var finalBasis = yawBasis * initialBasis;

        _cameraPivot.GlobalTransform = new Transform3D(
            finalBasis.Orthonormalized(),
            GlobalPosition
        );
    }

    private void UpdateCameraPitch(float rawLookYDelta)
    {
        if (_camera == null)
            return;

        var pitchChange = Mathf.DegToRad(-rawLookYDelta * MouseSensitivity * (InvertY ? -1f : 1f));

        _camera.RotateX(pitchChange);

        var cameraRotation = _camera.Rotation;
        cameraRotation.X = Mathf.Clamp(cameraRotation.X, MinPitchAngle, MaxPitchAngle);
        _camera.Rotation = cameraRotation;
    }

    private void ProcessWeaponBobbing(float delta)
    {
        _targetWeaponBobOffset = _targetWeaponBobOffset.Lerp(
            Vector3.Zero,
            delta * WeaponBobbingReturnSpeed
        );
        _currentWeaponBobOffset = _currentWeaponBobOffset.Lerp(
            _targetWeaponBobOffset,
            delta * WeaponBobbingFollowSpeed
        );
        _gun.Position = _initialWeaponLocalPosition + _currentWeaponBobOffset;
    }

    private void OnWeaponBobbingToggled(bool enabled)
    {
        _weaponBobbingEnabled = enabled;
        if (!_weaponBobbingEnabled && _gun != null)
        {
            _targetWeaponBobOffset = Vector3.Zero;
        }
    }

    private void ProcessBufferedJump()
    {
        if (!(_inputBuffer?.HasBufferedAction("jump") ?? false))
            return;
        if (_currentMovementController?.TryJump() ?? false)
        {
            _inputBuffer.ConsumeBufferedAction("jump");
        }
    }

    public Node3D GetCameraPivot() => _cameraPivot;

    public Gun GetGun() => _gun;

    public void TakeDamage(float amount)
    {
        if (_currentHealth <= 0)
            return;
        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        _audioManager = GetNode<AudioManager>("/root/AudioManager");
        _audioManager?.PlaySFX("res://Assets/Audio/slash_1.wav");
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (ProcessMode == ProcessModeEnum.Disabled)
            return;
        GD.Print("Player Died");
        EmitSignal(SignalName.Died);
        ProcessMode = ProcessModeEnum.Disabled;
        SetCollisionLayerValue(1, false);
        Visible = false;
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
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
        return false;
    }

    public void ApplyGunPowerup(PowerupType type, float multiplier, float duration)
    {
        if (_gun != null)
        {
            _gun.ApplyPowerup(type, multiplier, duration);
        }
    }

    private void OnMovementModeChanged(InputManager.MovementMode newMode) { }
}
