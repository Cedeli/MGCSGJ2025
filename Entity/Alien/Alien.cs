using System;
using Godot;

public partial class Alien : GravityEntity
{
    // Placeholder
    [Export]
    public float Health = 100.0f;

    [Export]
    public float AttackDamage = 10.0f;

    [Export]
    public float AttackRange = 5.0f;

    [Export]
    public float DetectionRadius = 50.0f;

    private Node3D _target;

    public override void _Ready()
    {
        base._Ready();
        GD.Print($"{Name} (Alien) is ready");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public void TakeDamage(float amount)
    {
        Health -= amount;
        GD.Print($"{Name} took {amount} damage Health: {Health}");
        if (Health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        GD.Print($"{Name} died");
        QueueFree();
    }
}
