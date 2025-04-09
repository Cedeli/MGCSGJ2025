using Godot;
using System;

public partial class Player : GravityEntity, IInputReceiver
{
    private const float MinPitchAngle = -Mathf.Pi / 2.0f + 0.01f;
    private const float MaxPitchAngle = Mathf.Pi / 2.0f - 0.01f;

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
        base._Ready();
        AddToGroup("input_receivers");
        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _planetaryMovement = new PlanetaryMovementController(this);
        _currentMovementController = _planetaryMovement;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        var fDelta = (float)delta;
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

    private void UpdateCameraOrientation()
    {
        if (_cameraPivot == null || _planetaryMovement == null) return;
        
        var targetUp = -GetGravityDirection();
        var currentBodyBasis = GlobalTransform.Basis;
        var planetAlignedBasis = CreateBasisFromUp(targetUp, -currentBodyBasis.Z);

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

    public Node3D GetCameraPivot()
    {
        return _cameraPivot;
    }

    private void OnMovementModeChanged(InputManager.MovementMode newMode)
    {
        GD.Print($"Player: Movement mode changed to {newMode}.");
    }
}