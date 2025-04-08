using Godot;
using System;
using System.Linq;
using System.Text.RegularExpressions;

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
    private Node3D _pivot;
    private Vector2 _mouseDelta;
    private float _mouseSens;
    private float _mouseXRotation;
    private float _mouseYRotation;
    // Ground
    private CelestialBody _ground;
    private CelestialBody[] _bodies;
    private Vector3 _surfacePos;
    // State
    private bool _isGrounded;
    //Forces
    private Vector3 _closestForce;
    

    public void SetPlayer(RigidBody3D player, float speed, float mass, float thrust, float autoOrientSpeed,
        float mouseSens, Vector2 mouseDelta, bool isGrounded, CelestialBody ground, Node3D pivot)
    {
        _player = player;
        _speed = speed;
        _mass = mass;
        _thrust = thrust;
        _autoOrientSpeed = autoOrientSpeed;

        _mouseSens = mouseSens;
        _mouseDelta = mouseDelta;
        _pivot = pivot;

        _isGrounded = isGrounded;
        _ground = ground;
        
    }
    public override void _Ready()
    {
        GD.Print("Player Controller Ready");
        GD.Print(GetTree().GetNodesInGroup("celestial_bodies"));
        
    }
    
    public override void _PhysicsProcess(double delta)
    {
        PlayerMovement();
        CameraMovement();

        var closestForceMagnitude = 0f;
        _closestForce = Vector3.Zero;

        foreach (Node planet in GetTree().GetNodesInGroup("celestial_bodies"))
        {
            if (planet is CelestialBody body)
            {
                var force = body.GetAccelerationAtPosition(GlobalPosition) * _mass;
                _player.ApplyCentralForce(force);

                if (body.GlobalPosition.DistanceTo(_player.GlobalPosition) < 2f * body.Radius)
                {
                    var magnitude = force.Length();
                    if (magnitude > closestForceMagnitude)
                    {
                        closestForceMagnitude = magnitude;
                        _closestForce = force;
                    }
                    
                }
            }
        }
        // Model Direction
    


        // stick to surface

    }
    private void PlayerMovement()
    {
        // Player movement
        var forward = -_player.GlobalTransform.Basis.Z;
        var left = -_player.GlobalTransform.Basis.X;
        var up = _player.GlobalTransform.Basis.Y;
        
        if (Input.IsActionPressed("Forward")) _movement += forward;
        if (Input.IsActionPressed("Backward")) _movement -= forward;
        if (Input.IsActionPressed("Left")) _movement += left;
        if (Input.IsActionPressed("Right")) _movement -= left;
        if (Input.IsActionPressed("Up")) _movement += up;
        if (Input.IsActionPressed("Down")) _movement -= up;
        
        // _movement = new Vector3(
        //     Input.GetActionStrength("right") - Input.GetActionStrength("left"),
        //     Input.GetActionStrength("jump") - Input.GetActionStrength("crouch"),
        //     Input.GetActionStrength("back") - Input.GetActionStrength("front")
        // ).Normalized();
        
        _velocity = _movement.Normalized() * _speed * _sprintSpeed;
        _player.ApplyCentralForce(_velocity);
        if (_movement != Vector3.Zero) _player.ApplyCentralForce(_thrust * _movement);
        
        if (_isGrounded && Input.IsActionJustReleased("Up")) _player.ApplyCentralImpulse(up);
    }

    private void CameraMovement()
    {

        float deltaX = _mouseDelta.X * _mouseSens;
        float deltaY = _mouseDelta.Y * _mouseSens;

        _mouseXRotation -= deltaY;
        _mouseXRotation = Mathf.Clamp(_mouseXRotation, -90f, 90f);
        
        _player.RotateY(Mathf.DegToRad(-deltaX));
        
        if (_pivot != null)
        {
            _pivot.RotationDegrees = new Vector3(_mouseXRotation, 0, 0);
        }

        // Reset mouse delta after processing
        _mouseDelta = Vector2.Zero;
    }

    private void AutoOrient(double delta)
    {
        var inZeroG = _closestForce == Vector3.Zero;

        if (inZeroG)
        {
            var dx = Mathf.Lerp(0, -_mouseYRotation, _autoOrientSpeed * (float)delta);
            _mouseYRotation += dx;
            
            _pivot.RotateX(Mathf.DegToRad(-dx));
            _player.Rotate(_pivot.GlobalTransform.Basis.X, Mathf.DegToRad(dx));
        }
        else
        {
            var upDirection = -_closestForce.Normalized();
            var orientation = new Quaternion(_player.GlobalTransform.Basis.Y, upDirection) 
                              * GlobalTransform.Basis.GetRotationQuaternion();
            if (_isGrounded)
            {
                _player.GlobalRotation = orientation.Normalized().GetEuler();
            }
            else
            {
                var rotation = _player.GlobalTransform.Basis.GetRotationQuaternion().Slerp(
                    orientation.Normalized(), _autoOrientSpeed * (float)delta);
                _player.GlobalRotation = rotation.GetEuler();
            }
        }
    }
    
    // Input Handler
    public override void _Input(InputEvent @event)
    {
        // adjust speed if player sprints
        _sprintSpeed = @event.IsActionPressed("Sprint")
            ? _sprintSpeed = 35.0f
            : _sprintSpeed = 2.0f;
        
        // Mouse Motion Capture
        if (@event is InputEventMouseMotion mouseMotion) _mouseDelta = mouseMotion.Relative;
    }
}
