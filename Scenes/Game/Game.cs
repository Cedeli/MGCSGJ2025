using System;
using System.Collections.Generic;
using Godot;

public partial class Game : Node3D
{
    [Signal]
    public delegate void RoundChangedEventHandler(int newRound);

    [Signal]
    public delegate void GameOverEventHandler(string reason, int finalRound, int finalScore);

    [Signal]
    public delegate void RoundTimerUpdateEventHandler(float timeRemaining);

    [Signal]
    public delegate void ScoreUpdatedEventHandler(int newScore);

    private enum GameState
    {
        Playing,
        GameOver,
        Paused,
    }

    [ExportGroup("Game Rules")] [Export] private float _roundDurationSeconds = 30.0f;

    [Export] private int _initialAlienCount = 5;

    [Export] private int _alienIncreasePerRound = 3;

    [Export] private int _alienScoreValue = 10;

    [ExportGroup("Scene References")] [Export]
    private PackedScene _alienScene;

    [ExportGroup("Item Spawning")] [Export]
    private PackedScene _healthItemScene;

    [Export] private PackedScene _ammoItemScene;

    [Export] private PackedScene _powerupItemScene;

    [Export] private PackedScene _scrapItemScene;

    [Export(PropertyHint.Range, "0, 10, 1")]
    private int _maxItemsPerRound = 5;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _itemSpawnChance = 0.85f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _healthItemProbability = 0.30f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _ammoItemProbability = 0.30f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _powerupItemProbability = 0.20f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _scrapItemProbability = 0.20f;

    private GameManager _gameManager;
    private AudioManager _audioManager;

    private Timer _roundTimer;
    private Timer _alienSpawnTimer;
    private Player _player;
    private Ship _ship;
    private GameState _currentState = GameState.Playing;
    private int _currentRound = 0;
    private int _currentScore = 0;
    private readonly Random _random = new();

    private ulong _gameStartTime = 0;
    private int _aliensKilled = 0;
    private int _aliensToSpawnThisRound = 0;
    private float _alienSpawnInterval = 1.0f;

    private const string PauseScenePath = "res://Scenes/Pause/Pause.tscn";
    private const string ResultScenePath = "res://Scenes/Result/Result.tscn";

    public int GetCurrentRound() => _currentRound;

    public int GetCurrentScore() => _currentScore;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        _roundTimer = new Timer
        {
            Name = "RoundTimer",
            WaitTime = _roundDurationSeconds,
            OneShot = false,
        };
        AddChild(_roundTimer);
        _roundTimer.Timeout += OnRoundTimerTimeout;

        _alienSpawnTimer = new Timer { Name = "AlienSpawnTimer", OneShot = false };
        AddChild(_alienSpawnTimer);
        _alienSpawnTimer.Timeout += OnAlienSpawnTimerTimeout;

        _player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
        _ship = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);

        if (_player == null)
            GD.PrintErr("Player not found");
        if (_ship == null)
            GD.PrintErr("Ship not found");
        if (_alienScene == null)
            GD.PrintErr("Alien scene not assigned");
        if (_healthItemScene == null)
            GD.PrintErr("HealthItem scene not assigned");
        if (_ammoItemScene == null)
            GD.PrintErr("AmmoItem scene not assigned");
        if (_powerupItemScene == null)
            GD.PrintErr("PowerupItem scene not assigned");
        if (_scrapItemScene == null)
            GD.PrintErr("ScrapItem scene not assigned");

        var totalProb =
            _healthItemProbability
            + _ammoItemProbability
            + _powerupItemProbability
            + _scrapItemProbability;
        if (Math.Abs(totalProb - 1.0f) > 0.01f)
        {
            GD.Print($"Item spawn probabilities sum to {totalProb} not be exactly 1.0");
        }

        if (_player != null) _player.Died += OnPlayerDied;
        if (_ship != null) _ship.Died += OnShipDied;
        _gameManager.SceneChanged += OnSceneChanged;
        _gameManager.PushScene("res://Scenes/HUD/HUD.tscn");

        StartGame();
    }

    public override void _Process(double delta)
    {
        if (_currentState != GameState.Playing)
            return;
        EmitSignal(SignalName.RoundTimerUpdate, (float)(_roundTimer?.TimeLeft ?? 0));
    }

    public override void _ExitTree()
    {
        if (_gameManager != null)
            _gameManager.SceneChanged -= OnSceneChanged;
        if (IsInstanceValid(_roundTimer))
            _roundTimer.Timeout -= OnRoundTimerTimeout;
        if (IsInstanceValid(_alienSpawnTimer))
            _alienSpawnTimer.Timeout -= OnAlienSpawnTimerTimeout;
        DisconnectEntitySignals();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
    }

    private void StartGame()
    {
        _currentState = GameState.Playing;
        _currentRound = 0;
        _currentScore = 0;
        _aliensKilled = 0;
        _aliensToSpawnThisRound = 0;
        _gameStartTime = Time.GetTicksMsec();
        EmitSignal(SignalName.ScoreUpdated, _currentScore);
        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        StartNewRound();
    }

    private void StartNewRound()
    {
        if (_currentState != GameState.Playing)
            return;

        _currentRound++;
        EmitSignal(SignalName.RoundChanged, _currentRound);

        _aliensToSpawnThisRound = _initialAlienCount + (_currentRound - 1) * _alienIncreasePerRound;
        _alienSpawnInterval =
            (_aliensToSpawnThisRound > 0)
                ? (_roundDurationSeconds * 0.95f) / _aliensToSpawnThisRound
                : 1.0f;

        GD.Print(
            $"Starting Round {_currentRound}: Spawning {_aliensToSpawnThisRound} aliens with interval {_alienSpawnInterval:F2}s"
        );

        _roundTimer.Start(_roundDurationSeconds);

        if (_aliensToSpawnThisRound > 0)
        {
            _alienSpawnTimer.WaitTime = _alienSpawnInterval;
            _alienSpawnTimer.Start();
            OnAlienSpawnTimerTimeout();
        }

        SpawnItems();
    }

    private void EndGame(string reason)
    {
        if (_currentState == GameState.GameOver)
            return;

        _currentState = GameState.GameOver;
        _roundTimer?.Stop();
        _alienSpawnTimer?.Stop();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        ulong endTime = Time.GetTicksMsec();
        ulong durationMillis = endTime - _gameStartTime;
        TimeSpan durationSpan = TimeSpan.FromMilliseconds(durationMillis);
        string durationFormatted =
            $"{durationSpan.Minutes:D2}:{durationSpan.Seconds:D2}.{durationSpan.Milliseconds / 100:D1}";

        EmitSignal(SignalName.GameOver, reason, _currentRound, _currentScore);
        DisconnectEntitySignals();
        ClearSpawnedEntities("alien");
        ClearSpawnedEntities("item");

        var parameters = new Dictionary<string, string>
        {
            { "finalScore", _currentScore.ToString() },
            { "finalRound", _currentRound.ToString() },
            { "reason", reason },
            { "timeSurvived", durationFormatted },
            { "enemiesKilled", _aliensKilled.ToString() },
        };
        _gameManager.SetParameters(parameters);
        _gameManager.ChangeScene(ResultScenePath);
    }

    private void AddScore(int amount)
    {
        if (_currentState != GameState.Playing)
            return;
        _currentScore += amount;
        EmitSignal(SignalName.ScoreUpdated, _currentScore);
    }

    private void OnAlienDied()
    {
        if (_currentState != GameState.Playing)
            return;
        _aliensKilled++;
        AddScore(_alienScoreValue);
    }

    private void DisconnectEntitySignals()
    {
        if (_player != null && IsInstanceValid(_player))
        {
            if (_player.IsConnected(Player.SignalName.Died, Callable.From(OnPlayerDied)))
                _player.Died -= OnPlayerDied;
        }

        if (_ship != null && IsInstanceValid(_ship))
        {
            if (_ship.IsConnected(Ship.SignalName.Died, Callable.From(OnShipDied)))
                _ship.Died -= OnShipDied;
        }

        ClearSignalsFromGroup("alien", Alien.SignalName.Died, Callable.From(OnAlienDied));
    }

    private void OnRoundTimerTimeout()
    {
        if (_currentState != GameState.Playing)
            return;
        _alienSpawnTimer.Stop();
        StartNewRound();
    }

    private void OnAlienSpawnTimerTimeout()
    {
        if (_currentState != GameState.Playing || _aliensToSpawnThisRound <= 0)
        {
            _alienSpawnTimer.Stop();
            return;
        }

        SpawnSingleAlien();
        _aliensToSpawnThisRound--;

        if (_aliensToSpawnThisRound > 0)
        {
            _alienSpawnTimer.WaitTime = _alienSpawnInterval;
            _alienSpawnTimer.Start();
        }
        else
        {
            _alienSpawnTimer.Stop();
            GD.Print($"Finished spawning aliens for round {_currentRound}.");
        }
    }

    private void OnPlayerDied() => EndGame("Player Died");

    private void OnShipDied() => EndGame("Ship Destroyed");

    private void OnSceneChanged(string scenePath, bool isPushing)
    {
        if (scenePath == PauseScenePath)
        {
            var pausing = isPushing;
            switch (pausing)
            {
                case true when _currentState == GameState.Playing:
                    _currentState = GameState.Paused;
                    _roundTimer?.SetPaused(true);
                    _alienSpawnTimer?.SetPaused(true);
                    ProcessMode = ProcessModeEnum.Disabled;
                    Input.SetMouseMode(Input.MouseModeEnum.Visible);
                    _audioManager?.SetBGMPaused(true);
                    break;
                case false when _currentState == GameState.Paused:
                    _currentState = GameState.Playing;
                    _roundTimer?.SetPaused(false);
                    _alienSpawnTimer?.SetPaused(false);
                    ProcessMode = ProcessModeEnum.Inherit;
                    Input.SetMouseMode(Input.MouseModeEnum.Captured);
                    _audioManager?.SetBGMPaused(false);
                    break;
            }
        }
    }

    private void SpawnSingleAlien()
    {
        if (_player == null || _alienScene == null || _currentState != GameState.Playing)
            return;

        var newAlien = _alienScene.Instantiate<Alien>();
        if (newAlien == null) return;
        newAlien.Died += OnAlienDied;
        newAlien.AddToGroup("alien");

        var angle = (float)(_random.NextDouble() * Math.PI * 2);
        var distance = 30.0f + (float)(_random.NextDouble() * 20.0);
        var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;

        var playerUp = -_player.GetGravityDirection().Normalized();
        if (playerUp.LengthSquared() < 0.01f)
            playerUp = Vector3.Up;

        var spawnPos = _player.GlobalPosition + offset + playerUp * 3.0f;

        AddChild(newAlien);
        newAlien.GlobalPosition = spawnPos;
    }

    private void SpawnItems()
    {
        var itemsToAttempt = _random.Next(1, _maxItemsPerRound + 1);
        for (var i = 0; i < itemsToAttempt; i++)
        {
            if (_random.NextDouble() < _itemSpawnChance)
            {
                SpawnSingleItem();
            }
        }
    }

    private void SpawnSingleItem()
    {
        if (_player == null)
            return;

        var itemSceneToSpawn = ChooseItemScene();

        var newItem = itemSceneToSpawn?.Instantiate<Item>();
        if (newItem == null) return;
        newItem.AddToGroup("item");

        var angle = (float)(_random.NextDouble() * Math.PI * 2);
        var distance = 8.0f + (float)(_random.NextDouble() * 7.0);
        var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;

        var playerUp = -_player.GetGravityDirection().Normalized();
        if (playerUp.LengthSquared() < 0.01f)
            playerUp = Vector3.Up;

        var spawnPos = _player.GlobalPosition + offset + playerUp * 1.0f;

        AddChild(newItem);
        newItem.GlobalPosition = spawnPos;
    }

    private PackedScene ChooseItemScene()
    {
        var roll = _random.NextDouble();
        var cumulativeProbability = 0f;

        cumulativeProbability += _healthItemProbability;
        if (roll < cumulativeProbability && _healthItemScene != null)
            return _healthItemScene;

        cumulativeProbability += _ammoItemProbability;
        if (roll < cumulativeProbability && _ammoItemScene != null)
            return _ammoItemScene;

        cumulativeProbability += _scrapItemProbability;
        if (roll < cumulativeProbability && _scrapItemScene != null)
            return _scrapItemScene;

        cumulativeProbability += _powerupItemProbability;
        if (roll < cumulativeProbability && _powerupItemScene != null)
            return _powerupItemScene;

        if (_scrapItemScene != null)
            return _scrapItemScene;
        if (_healthItemScene != null)
            return _healthItemScene;
        if (_ammoItemScene != null)
            return _ammoItemScene;
        if (_powerupItemScene != null)
            return _powerupItemScene;

        GD.PrintErr("No item scenes available to spawn");
        return null;
    }

    private T GetNodeFromGroupHelper<T>(string group)
        where T : Node
    {
        var nodes = GetTree().GetNodesInGroup(group);
        return nodes.Count > 0 && nodes[0] is T typedNode && IsInstanceValid(typedNode)
            ? typedNode
            : null;
    }

    private void ClearSpawnedEntities(string groupName)
    {
        foreach (var node in GetTree().GetNodesInGroup(groupName))
        {
            if (!IsInstanceValid(node))
                continue;
            if (
                node is Alien alien
                && alien.IsConnected(Alien.SignalName.Died, Callable.From(OnAlienDied))
            )
            {
                alien.Died -= OnAlienDied;
            }

            node.QueueFree();
        }
    }

    private void ClearSignalsFromGroup(string groupName, StringName signalName, Callable callable)
    {
        foreach (var node in GetTree().GetNodesInGroup(groupName))
        {
            if (IsInstanceValid(node) && node.IsConnected(signalName, callable))
            {
                node.Disconnect(signalName, callable);
            }
        }
    }
}