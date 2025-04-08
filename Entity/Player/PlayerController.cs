using Godot;
using System;

public partial class PlayerController : Node3D
{

    private RigidBody3D _player;
    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _direction;
    private float _sprintSpeed;
    private float _speed;
    private float _mass;

    public void SetPlayer(RigidBody3D player, float speed, float mass)
    {
        _player = player;
        _speed = speed;
        _mass = mass;
    }
    public override void _Ready()
    {
        GD.Print("Player Controller Ready");
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Player movement
        _direction = new Vector3(
            Input.GetActionStrength("right") - Input.GetActionStrength("left"),
            Input.GetActionStrength("crouch") - Input.GetActionStrength("jump"),
            Input.GetActionStrength("back") - Input.GetActionStrength("front")
        );

        _velocity = _direction * _speed * _sprintSpeed;
    
        _player.ApplyCentralForce(_velocity);
    }

    public override void _Input(InputEvent @event)
    {
        // adjust speed if player sprints
        _sprintSpeed = @event.IsActionPressed("sprint")
            ? _sprintSpeed = 35.0f
            : _sprintSpeed = 2.0f;
    }
}
