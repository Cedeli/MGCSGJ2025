using System;
using Godot;

public enum ItemType
{
    Health,
    Ammo,
    Scrap,
    Quest,
}

public partial class Item : GravityEntity
{
    // Placeholder
    [Export]
    public ItemType Type = ItemType.Scrap;

    public override void _Ready()
    {
        base._Ready();
        GD.Print($"{Name} (Item: {Type}, Value: {Value}) is ready.");

        Sleeping = true;
        LinearDamp = 0.9f;
        AngularDamp = 0.9f;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public void Pickup()
    {
        GD.Print($"Item {Name} picked up");
        QueueFree();
    }
}
