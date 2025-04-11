using System;
using Godot;

public abstract partial class Item : GravityEntity
{
    private GameManager _gameManager;
    private AudioManager _audioManager;

    private Game _gameScene;
    private Player _player;
    private Ship _ship;
    [Export] private Node3D _visualNode;
    [Export] private MeshInstance3D _lootBeam;

    private BaseMaterial3D _lootBeamMaterial;

    private const string CollectibleSfx = "res://Assets/Audio/collectible_1.wav";

    [ExportGroup("Visual Effects")]
    [Export] public float BobAmplitude = 0.1f;
    [Export] public float BobFrequency = 0.3f;
    [Export] public float SpinSpeed = 1.5f;
    [Export] public float FadeFrequency = 1.0f;

    private float _timeAccumulator;
    private Vector3 _initialVisualLocalPosition = Vector3.Zero;

    public override void _Ready()
    {
        base._Ready();
        BodyEntered += OnBodyEntered;
        ContactMonitor = true;
        MaxContactsReported = 1;

        if (_visualNode != null)
        {
            _initialVisualLocalPosition = _visualNode.Position;
        }

        if (_lootBeam != null)
        {
            var originalMaterial = _lootBeam.GetActiveMaterial(0);
            if (originalMaterial != null)
            {
                _lootBeamMaterial = (BaseMaterial3D)originalMaterial.Duplicate();
                _lootBeam.MaterialOverride = _lootBeamMaterial;
            }
            else
            {
                GD.PrintErr($"Item '{Name}': Loot beam mesh instance does not have a material assigned.");
            }
        }
        else
        {
            GD.PrintErr($"Item '{Name}': _lootBeam MeshInstance3D is not assigned.");
        }


        _audioManager = GetNode<AudioManager>("/root/AudioManager");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        _timeAccumulator += (float)delta;
        
        if (_visualNode != null)
        {
            var bobOffset =
                BobAmplitude * Mathf.Sin(_timeAccumulator * BobFrequency * Mathf.Pi * 2.0f);
            var newVisualPos = _initialVisualLocalPosition;
            newVisualPos.Y += bobOffset;
            _visualNode.Position = newVisualPos;

            _visualNode.RotateY(SpinSpeed * (float)delta);
        }

        if (_lootBeamMaterial == null) return;
        var alpha = (Mathf.Sin(_timeAccumulator * FadeFrequency * Mathf.Pi * 2.0f) + 1.0f) / 2.0f;
        if (_lootBeamMaterial is not StandardMaterial3D standardMaterial) return;
        var currentColor = standardMaterial.AlbedoColor;
        currentColor.A = alpha;
        standardMaterial.AlbedoColor = currentColor;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is not Player player) return;
        GD.Print($"{Name} collided with Player {player.Name}");
        if (!ApplyEffect(player)) return;
        _audioManager.PlaySFX(CollectibleSfx);
        GD.Print($"Effect of {Name} applied to {player.Name} destroying item");
        QueueFree();
    }

    protected abstract bool ApplyEffect(Player player);
}