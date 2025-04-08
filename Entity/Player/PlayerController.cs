using Godot;
using System;

public partial class PlayerController : Node3D
{
    [Export] private Player _player;

    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _direction;
    private float _sprintSpeed;

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

        // if (_direction != Vector3.Zero)
        // {
        //     _direction = _direction.Normalized();
        //     GetNode<Node3D>("Pivot").Basis = Basis.LookingAt(_direction);
        // }
 
        _velocity = _direction.LimitLength();

        _player.Velocity = _velocity != Vector3.Zero
            ? _player.Velocity.Lerp(_velocity * _player.Speed * 2, _sprintSpeed)
            : _player.Velocity.Lerp(Vector3.Zero, _sprintSpeed);

        _player.MoveAndSlide();
    }

    public override void _Input(InputEvent @event)
    {
        // adjust speed if player sprints
        _sprintSpeed = @event.IsActionPressed("sprint")
            ? _sprintSpeed = 35.0f
            : _sprintSpeed = 1.0f;
    }
}
