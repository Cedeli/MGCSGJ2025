using Godot;
using System;

[Tool]
[GlobalClass]
public partial class SimplexNoiseSettings : Resource
{
    [Signal]
    public delegate void ChangedEventHandler();

    private int _numLayers = 4;

    [Export]
    public int NumLayers
    {
        get => _numLayers;
        set
        {
            if (_numLayers == value) return;
            _numLayers = value;
            EmitSignal(SignalName.Changed);
        }
    }

    private float _lacunarity = 2.0f;

    [Export]
    public float Lacunarity
    {
        get => _lacunarity;
        set
        {
            if (Mathf.IsEqualApprox(_lacunarity, value)) return;
            _lacunarity = value;
            EmitSignal(SignalName.Changed);
        }
    }

    private float _persistence = 0.5f;

    [Export]
    public float Persistence
    {
        get => _persistence;
        set
        {
            if (Mathf.IsEqualApprox(_persistence, value)) return;
            _persistence = value;
            EmitSignal(SignalName.Changed);
        }
    }

    private float _scale = 1.0f;

    [Export]
    public float Scale
    {
        get => _scale;
        set
        {
            if (Mathf.IsEqualApprox(_scale, value)) return;
            _scale = value;
            EmitSignal(SignalName.Changed);
        }
    }

    private float _elevation = 1.0f;

    [Export]
    public float Elevation
    {
        get => _elevation;
        set
        {
            if (Mathf.IsEqualApprox(_elevation, value)) return;
            _elevation = value;
            EmitSignal(SignalName.Changed);
        }
    }

    private float _verticalShift = 0.0f;

    [Export]
    public float VerticalShift
    {
        get => _verticalShift;
        set
        {
            if (Mathf.IsEqualApprox(_verticalShift, value)) return;
            _verticalShift = value;
            EmitSignal(SignalName.Changed);
        }
    }

    private Vector3 _offset = Vector3.Zero;

    [Export]
    public Vector3 Offset
    {
        get => _offset;
        set
        {
            if (_offset == value) return;
            _offset = value;
            EmitSignal(SignalName.Changed);
        }
    }

    public float[] GetNoiseParams(RandomNumberGenerator rng)
    {
        rng ??= new RandomNumberGenerator();

        var seededOffset = new Vector3(rng.Randf(), rng.Randf(), rng.Randf()) * rng.Randf() * 10000.0f;
        var finalOffset = seededOffset + Offset;

        return
        [
            finalOffset.X,
            finalOffset.Y,
            finalOffset.Z,
            NumLayers,
            Persistence,
            Lacunarity,
            Scale,
            Elevation,
            VerticalShift,
            0.0f,
            0.0f,
            0.0f
        ];
    }
}