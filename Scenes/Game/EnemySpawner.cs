using Godot;
using System;

public partial class EnemySpawner : Node
{
    [Signal]
    public delegate void AlienSpawnedEventHandler(Alien alienInstance);

    [ExportGroup("Spawning Rules")]
    [Export] private PackedScene _alienScene;
    [Export] private int _initialAlienCount = 5;
    [Export] private int _alienIncreasePerRound = 3;
    [Export] private float _spawnDistanceMin = 30.0f;
    [Export] private float _spawnDistanceMax = 50.0f;
    [Export] private float _spawnHeightOffset = 3.0f;
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _spawnTimePercentageOfRound = 0.95f;

    private Timer _spawnTimer;
    private Player _player;
    private int _aliensToSpawnThisRound = 0;
    private float _alienSpawnInterval = 1.0f;
    private bool _isSpawning = false;
    private readonly Random _random = new();

    public override void _Ready()
    {
        _spawnTimer = new Timer { Name = "SpawnTimer", OneShot = false };
        AddChild(_spawnTimer);
        _spawnTimer.Timeout += OnSpawnTimerTimeout;

        _player = GetTree().GetFirstNodeInGroup(Player.PlayerGroup) as Player;
        if (_player == null)
        {
            GD.PrintErr($"{nameof(EnemySpawner)}: Player not found in group '{Player.PlayerGroup}'. Spawning disabled.");
        }
        if (_alienScene == null)
        {
            GD.PrintErr($"{nameof(EnemySpawner)}: Alien scene not assigned. Spawning disabled.");
        }
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_spawnTimer))
        {
             _spawnTimer.Timeout -= OnSpawnTimerTimeout;
        }
    }

    public void StartSpawningForRound(int round, float roundDurationSeconds)
    {
        if (_player == null || _alienScene == null) return;

        _aliensToSpawnThisRound = _initialAlienCount + (round - 1) * _alienIncreasePerRound;
        _alienSpawnInterval =
            (_aliensToSpawnThisRound > 0)
                ? (roundDurationSeconds * _spawnTimePercentageOfRound) / _aliensToSpawnThisRound
                : 1.0f;

        GD.Print(
            $"Spawner: Round {round}: Spawning {_aliensToSpawnThisRound} aliens with interval {_alienSpawnInterval:F2}s"
        );

        if (_aliensToSpawnThisRound > 0)
        {
            _isSpawning = true;
            _spawnTimer.WaitTime = _alienSpawnInterval;
            _spawnTimer.Start();
            OnSpawnTimerTimeout();
        }
        else
        {
            _isSpawning = false;
            _spawnTimer.Stop();
        }
    }

    public void StopSpawning()
    {
        _isSpawning = false;
        _spawnTimer?.Stop();
        _aliensToSpawnThisRound = 0;
    }

     public void SetPaused(bool paused)
    {
        if (!IsInstanceValid(_spawnTimer)) return;

        if (paused && _spawnTimer.IsStopped()) return;

        _spawnTimer.Paused = paused;
    }


    private void OnSpawnTimerTimeout()
    {
        if (!_isSpawning || _aliensToSpawnThisRound <= 0 || _player == null || _alienScene == null)
        {
            StopSpawning();
            return;
        }

        SpawnSingleAlien();
        _aliensToSpawnThisRound--;

        if (_aliensToSpawnThisRound > 0)
        {
            _spawnTimer.WaitTime = _alienSpawnInterval;
            _spawnTimer.Start();
        }
        else
        {
            StopSpawning();
            GD.Print("Spawner: Finished spawning aliens for this round.");
        }
    }

    private void SpawnSingleAlien()
    {
        var newAlien = _alienScene.Instantiate<Alien>();
        if (newAlien == null) return;

        newAlien.AddToGroup("alien");

        var angle = (float)(_random.NextDouble() * Math.PI * 2);
        var distance = _spawnDistanceMin + (float)(_random.NextDouble() * (_spawnDistanceMax - _spawnDistanceMin));
        var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;

        var playerUp = -_player.GetGravityDirection().Normalized();
        if (playerUp.LengthSquared() < 0.01f)
            playerUp = Vector3.Up;

        var spawnPos = _player.GlobalPosition + offset + playerUp * _spawnHeightOffset;

        GetParent().AddChild(newAlien);
        newAlien.GlobalPosition = spawnPos;

        EmitSignal(SignalName.AlienSpawned, newAlien);
    }
}