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
    [Export]
    public ItemType Type = ItemType.Scrap;

    [Export]
    public int Value = 1;

    public override void _Ready()
    {
        base._Ready();
        GD.Print($"{Name} (Item: {Type}, Value: {Value}) is ready");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public void Pickup()
    {
        GD.Print($"Item {Name} picked up"); // future implementation
        QueueFree();
    }
}
