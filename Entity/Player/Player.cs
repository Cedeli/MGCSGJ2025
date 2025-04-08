using Godot;
using System;

public partial class Player : RigidBody3D
{
    // Player stats set this into a global stat sheet if there are multiple player models
    [Export] public float Speed { get; set; } = 35.5f;
    [Export] public float Mass { get; set; } = 1f;
    
    [Export] private PlayerController _controller;


    private Vector3 _direction;

    public override void _Ready()
    {
        GD.Print("Player Ready!");
        _controller.SetPlayer(this, Speed, Mass);
    }
}
