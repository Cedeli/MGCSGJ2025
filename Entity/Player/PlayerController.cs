using Godot;
using System;

public partial class PlayerController : Node3D
{

    private RigidBody3D _player;
    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _movement;
    // Player Stats
    private float _sprintSpeed;
    private float _speed;
    private float _mass;
    private float _thrust;
    private float _autoOrientSpeed;
    // For Aiming
    private float _mouseSens;
    private Vector2 _mouseDelta;
    private float _mouseXRotation;
    // Ground
    private CelestialBody _ground;
    private Vector3 _surfacePos;

    private float _mouseYRotation;
    // State
    private bool _isGrounded;
    

    public void SetPlayer(RigidBody3D player, float speed, float mass, float thrust, float autoOrientSpeed,
        float mouseSens, Vector2 mouseDelta, bool isGrounded, CelestialBody ground)
    {
        _player = player;
        _speed = speed;
        _mass = mass;
        _thrust = thrust;
        _autoOrientSpeed = autoOrientSpeed;

        _mouseSens = mouseSens;
        _mouseDelta = mouseDelta;

        _isGrounded = isGrounded;
        _ground = ground;
    }
    public override void _Ready()
    {
        GD.Print("Player Controller Ready");
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Player movement
        var forward = -GlobalTransform.Basis.Z;
        var left = -GlobalTransform.Basis.X;
        var up = GlobalTransform.Basis.Y;
        
        if (Input.IsActionPressed("front")) _movement += forward;
        if (Input.IsActionPressed("back")) _movement -= forward;
        if (Input.IsActionPressed("left")) _movement += left;
        if (Input.IsActionPressed("right")) _movement -= left;
        if (Input.IsActionPressed("jump")) _movement += up;
        if (Input.IsActionPressed("crouch")) _movement -= up;
        
        // _movement = new Vector3(
        //     Input.GetActionStrength("right") - Input.GetActionStrength("left"),
        //     Input.GetActionStrength("jump") - Input.GetActionStrength("crouch"),
        //     Input.GetActionStrength("back") - Input.GetActionStrength("front")
        // ).Normalized();
        
        _velocity = _movement.Normalized() * _speed * _sprintSpeed;
        _player.ApplyCentralForce(_velocity);
        if (_movement != Vector3.Zero) _player.ApplyCentralForce(_thrust * _movement);
        
        // Model Direction

        // _mouseXRotation = _mouseDelta.X * _mouseSens;
        // _mouseYRotation = _mouseDelta.Y * _mouseSens;
        // // Formula to prevent Over Rotation
        // // _mouseYRotation = Mathf.Clamp(_mouseYRotation - _mouseDelta.Y * _mouseSens, -90f, 90f);
        //
        // _player.RotateX(_player.GlobalPosition.AngleTo( new Vector3(_mouseXRotation, _mouseYRotation, 0)));
        // var yaw = new Basis(Vector3.Up, Mathf.DegToRad(_mouseXRotation));
        // //var pitch = new Basis(Vector3.Right, Mathf.DegToRad(_mouseYRotation));
        //
        // //_player.GlobalTransform = new Transform3D(yaw, _player.GlobalTransform.Origin);
        
        // stick to surface

        if (_isGrounded && _ground != null && _ground.GravitationalConstant < 0.2f)
        {
            _player.GlobalPosition = _ground.ToGlobal(_surfacePos);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // adjust speed if player sprints
        _sprintSpeed = @event.IsActionPressed("sprint")
            ? _sprintSpeed = 35.0f
            : _sprintSpeed = 2.0f;
        
        // Player Thruster
        if (_isGrounded && @event.IsActionReleased("jump")) 
            _player.ApplyCentralImpulse(Input.GetActionStrength("jump") * _movement);
        
        // Mouse Motion Capture
        if (@event is InputEventMouseMotion mouseMotion) _mouseDelta = mouseMotion.Relative;
    }
}
