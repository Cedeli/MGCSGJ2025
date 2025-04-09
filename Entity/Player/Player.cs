using Godot;
using System;

public partial class Player : RigidBody3D, IInputReceiver
{
    private const float MinPitchAngle = -Mathf.Pi / 2.0f + 0.01f;
    private const float MaxPitchAngle = Mathf.Pi / 2.0f - 0.01f;

    [ExportGroup("Movement")] [Export] public float MoveSpeed = 5.0f;
    [Export] public float JumpImpulse = 10.0f;

    [ExportGroup("Components")] [Export] private ShapeCast3D _groundCast;

    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _groundCastLength = 0.5f;

    [Export] private Node3D _cameraPivot;
    [Export] private Camera3D _camera;

    [ExportGroup("Input")] [Export(PropertyHint.Range, "0.01,1.0")]
    public float MouseSensitivity = 0.25f;

    [Export] public bool InvertY;

    private MovementController _currentMovementController;
    private PlanetaryMovementController _planetaryMovement;

    private InputManager _inputManager;
    private InputBuffer _inputBuffer;

    private Vector2 _movementInput;
    private float _yawAngle;

    public override void _Ready()
    {
        AddToGroup("input_receivers");

        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _planetaryMovement = new PlanetaryMovementController(this, _groundCast, _cameraPivot, MoveSpeed, JumpImpulse);
        _currentMovementController = _planetaryMovement;

        ConfigureRigidBody();
        UpdateGroundCastTarget();
    }

    public override void _PhysicsProcess(double delta)
    {
        var fDelta = (float)delta;
        UpdateGroundCastTarget();
        UpdateCameraOrientation();

        _currentMovementController?.PhysicsProcess(fDelta, _movementInput);

        ProcessBufferedJump();
    }

    public void OnMoveInput(Vector2 direction)
    {
        _movementInput = direction.LimitLength();
    }

    public void OnLookInput(Vector2 lookDelta)
    {
        _yawAngle -= Mathf.DegToRad(lookDelta.X * MouseSensitivity);
        _yawAngle = Mathf.Wrap(_yawAngle, -Mathf.Pi, Mathf.Pi);

        if (_camera != null)
        {
            UpdateCameraPitch(lookDelta.Y);
        }
        else
        {
            GD.PrintErr("Player: Camera node not assigned. Cannot process pitch input.");
        }
    }

    public void SetInputBuffer(InputBuffer buffer)
    {
        _inputBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        GD.Print("Player: InputBuffer assigned.");
    }

    public void SetInputManager(InputManager manager)
    {
        _inputManager = manager ?? throw new ArgumentNullException(nameof(manager));
        GD.Print("Player: InputManager assigned.");
    }

    private void ConfigureRigidBody()
    {
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 4;

        LinearDamp = 0.1f;
        AngularDamp = 0.8f;

        AxisLockLinearX = false;
        AxisLockLinearY = false;
        AxisLockLinearZ = false;
        AxisLockAngularX = false;
        AxisLockAngularY = false;
        AxisLockAngularZ = false;
    }

    private void UpdateGroundCastTarget()
    {
        var globalDownDirection = _currentMovementController?.GetGravityDirection() ?? Vector3.Down;
        globalDownDirection = globalDownDirection.Normalized();
        
        var globalTargetPoint = _groundCast.GlobalPosition + globalDownDirection * _groundCastLength;
        _groundCast.TargetPosition = _groundCast.ToLocal(globalTargetPoint);
    }

    private void UpdateCameraOrientation()
    {
        if (_cameraPivot == null || _planetaryMovement == null) return;

        var gravityDir = _planetaryMovement.GetGravityDirection();
        var targetUp = -gravityDir;

        var currentBodyBasis = GlobalTransform.Basis;
        var planetAlignedBasis =
            PlanetaryMovementController.CreateBasisFromUp(targetUp, -currentBodyBasis.Z);

        var yawRotation = Basis.Identity.Rotated(targetUp, _yawAngle);

        var finalPivotBasis = yawRotation * planetAlignedBasis;

        _cameraPivot.GlobalTransform = new Transform3D(finalPivotBasis.Orthonormalized(), _cameraPivot.GlobalPosition);
    }

    private void UpdateCameraPitch(float lookYDelta)
    {
        var pitchChange = Mathf.DegToRad(-lookYDelta * MouseSensitivity * (InvertY ? -1f : 1f));

        _camera.RotateX(pitchChange);

        var cameraRotation = _camera.Rotation;
        cameraRotation.X = Mathf.Clamp(cameraRotation.X, MinPitchAngle, MaxPitchAngle);
        _camera.Rotation = cameraRotation;
    }

    private void ProcessBufferedJump()
    {
        if (!(_inputBuffer?.HasBufferedAction("jump") ?? false)) return;
        if (_currentMovementController?.TryJump() ?? false)
        {
            _inputBuffer.ConsumeBufferedAction("jump");
        }
    }

    private void OnMovementModeChanged(InputManager.MovementMode newMode)
    {
        GD.Print($"Player: Movement mode changed to {newMode}.");
    }
}