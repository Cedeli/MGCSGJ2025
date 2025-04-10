using System;
using Godot;

public abstract partial class Item : GravityEntity
{
    // Services
    private GameManager _gameManager;
    private AudioManager _audioManager;

    // Node References
    private Game _gameScene;
    private Player _player;
    private Ship _ship;

    private const string COLLECTIBLE_SFX = "res://Assets/Audio/collectible_1.wav";

    public override void _Ready()
    {
        base._Ready();
        BodyEntered += OnBodyEntered;
        ContactMonitor = true;
        MaxContactsReported = 1;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Player player)
        {
            GD.Print($"{Name} collided with Player {player.Name}");
            if (ApplyEffect(player))
            {
                _audioManager = GetNode<AudioManager>("/root/AudioManager");
                _audioManager.PlaySFX(COLLECTIBLE_SFX);
                GD.Print($"Effect of {Name} applied to {player.Name}, Destroying item");
                QueueFree();
            }
            else
            {
                GD.Print($"Could not apply effect of {Name} to {player.Name}");
                QueueFree();
            }
        }
    }

    protected abstract bool ApplyEffect(Player player);
}
