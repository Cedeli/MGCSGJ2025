using Godot;
using System;

public partial class PlayerController : Node3D
{
    [Export] private Player _player;

    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _direction = Vector3.Zero;

    public override void _Ready()
    {
        GD.Print("Player Controller Ready");
    }
    
}
