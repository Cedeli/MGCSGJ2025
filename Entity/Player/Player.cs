using Godot;
using System;

public partial class Player : GravityEntity, IInputReceiver
{
    private const float MinPitchAngle = -Mathf.Pi / 2.0f + 0.01f;
    private const float MaxPitchAngle = Mathf.Pi / 2.0f - 0.01f;

    [Export] private Node3D _cameraPivot;
    [Export] private Camera3D _camera;

    [ExportGroup("Input")]
    [Export(PropertyHint.Range, "0.01,1.0")]
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

        if (_cameraPivot == null || _camera == null)
        {
            GD.PushError("Player: Camera Pivot or Camera node is not assigned in the inspector!");
            SetProcess(false);
            return;
        }
        
        _planetaryMovement = new PlanetaryMovementController(this);
        _currentMovementController = _planetaryMovement;
        
        if (_inputManager == null)
        {
            GD.PushError("Player: Input Manager does not exist!");
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
    }

    public void SetInputBuffer(InputBuffer buffer)
    {
        _inputBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        GD.Print("Player: InputBuffer assigned.");
    }

    public void SetInputManager(InputManager manager)
    {
        _inputManager = manager ?? throw new ArgumentNullException(nameof(manager));
    }
    
    private void UpdateCameraOrientation()
    {
        if (_cameraPivot == null) return;
        
        var targetUp = -GetGravityDirection();
        
        var targetPivotBasis = Basis.Identity;
        targetPivotBasis.Y = targetUp;
        
        var referenceForward = -Vector3.Forward.Slide(targetUp).Normalized();
        
        if (referenceForward.LengthSquared() < 0.01f)
        {
            referenceForward = Vector3.Right.Slide(targetUp).Normalized();
        }
        
        targetPivotBasis.X = targetUp.Cross(referenceForward).Normalized();
        targetPivotBasis.Z = targetPivotBasis.X.Cross(targetUp).Normalized();
        
        targetPivotBasis = targetPivotBasis.Rotated(targetPivotBasis.Y, _yawAngle);

        _cameraPivot.GlobalTransform = new Transform3D(targetPivotBasis.Orthonormalized(), GlobalPosition);
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
        if ((_inputBuffer?.HasBufferedAction("jump") ?? false) &&
            (_currentMovementController?.TryJump() ?? false))
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