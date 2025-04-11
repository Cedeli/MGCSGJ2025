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

	private const string CollectibleSfx = "res://Assets/Audio/collectible_1.wav";

	[ExportGroup("Visual Effects")] [Export]
	public float BobAmplitude = 0.1f;

	[Export] public float BobFrequency = 0.3f;

	[Export] public float SpinSpeed = 1.5f;

	private float _timeAccumulator = 0.0f;
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
		else
		{
			GD.PrintErr($"Item '{Name}': could not find MeshInstance3D child ");
		}

		_audioManager = GetNode<AudioManager>("/root/AudioManager");
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (_visualNode != null)
		{
			_timeAccumulator += (float)delta;
			float bobOffset =
				BobAmplitude * Mathf.Sin(_timeAccumulator * BobFrequency * Mathf.Pi * 2.0f);
			Vector3 newVisualPos = _initialVisualLocalPosition;
			newVisualPos.Y += bobOffset;
			_visualNode.Position = newVisualPos;

			_visualNode.RotateY(SpinSpeed * (float)delta);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body is Player player)
		{
			GD.Print($"{Name} collided with Player {player.Name}");
			if (ApplyEffect(player))
			{
				_audioManager.PlaySFX(CollectibleSfx);
				GD.Print($"Effect of {Name} applied to {player.Name} destroying item");
				QueueFree();
			}
			else
			{
			}
		}
	}

	protected abstract bool ApplyEffect(Player player);
}
