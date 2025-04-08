using Godot;
using System;

public partial class Player : RigidBody3D
{
    // Player stats set this into a global stat sheet if there are multiple player models
    [ExportCategory("Stats")]
    [Export] private float _speed { get; set; } = 35.5f;
    [Export] private float _thrust = 1.0f;
    [Export] private float _orientSpeed = 0.5f;

    // For Aiming
    [ExportCategory("Settings")] 
    [Export] private float _mouseSens = 0.5f;
    private Vector2 _mouseDelta;
    private float _cameraXRotation;
    private float _cameraYRotation;
    
    
    [ExportCategory("Nodes")]
    [Export] private PlayerController _controller;
    [Export] private Node3D _cameraPivot;
    [Export] private RayCast3D _groundCast;


    private bool IsGrounded => _groundCast.IsColliding();
    private CelestialBody Ground => _groundCast.GetCollider() as CelestialBody;
    

    public override void _Ready()
    {
        GD.Print("Player Ready!");
        _controller.SetPlayer(this, _speed, Mass, _thrust, _orientSpeed, _mouseSens, _mouseDelta, IsGrounded,
            Ground);
    }
}
