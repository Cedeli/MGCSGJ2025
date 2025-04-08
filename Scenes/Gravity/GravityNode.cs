using Godot;
using System;

public partial class GravityNode : Area3D
{
    [ExportCategory("Gravity Area Parameters")]
    [Export] private RigidBody3D _object;
    [Export] private float _xScale;
    [Export] private float _yScale;
    [Export] private float _zScale;
    [Export] private float _gravity;

    private GravityShape _shape;
    

    public override void _Ready()
    {
        GD.Print("Gravity Area Ready!");
        SetGravity(_gravity);
        _shape.SetParameters(_xScale, _yScale, _zScale); 
        
    }
    
}
