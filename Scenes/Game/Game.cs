using System;
using System.Collections.Generic;
using Godot;

public partial class Game : Node3D
{
    // --- Signals ---
    [Signal]
    public delegate void RoundChangedEventHandler(int newRound);

    [Signal]
    public delegate void GameOverEventHandler(string reason, int finalRound);

    [Signal]
    public delegate void RoundTimerUpdateEventHandler(float timeRemaining);

    // --- Enums ---
    private enum GameState
    {
        Playing,
        GameOver,
    }

    // --- Exports ---
    [ExportGroup("Game Rules")]
    [Export]
    private float RoundDurationSeconds = 30.0f;

    [Export]
    private int InitialAlienCount = 5;

    [Export]
    private int AlienIncreasePerRound = 3;

    [ExportGroup("Scene References")]
    [Export]
    private PackedScene _alienScene;

    // --- Services ---
    private GameManager _gameManager;
    private AudioManager _audioManager;

    // --- Internal State ---
    private Timer _roundTimer;
    private Player _player;
    private Ship _ship;
    private GameState _currentState = GameState.Playing;
    private int _currentRound = 0;

    // --- Constants ---
    private const string PAUSE_SCENE_PATH = "res://Scenes/Pause/Pause.tscn";
    private const string RESULT_SCENE_PATH = "res://Scenes/Result/Result.tscn";

    public int GetCurrentRound() => _currentRound;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        _roundTimer = new Timer
        {
            Name = "RoundTimer",
            WaitTime = RoundDurationSeconds,
            OneShot = false,
        };
        AddChild(_roundTimer);
        _roundTimer.Timeout += OnRoundTimerTimeout;

        _player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
        _ship = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);

        if (_player == null)
        {
            GD.PrintErr($"Player not found in group '{Player.PlayerGroup}'.");
            SetProcess(false);
            return;
        }
        if (_ship == null)
        {
            GD.PrintErr($"Ship not found in group '{Ship.ShipGroup}'.");
            SetProcess(false);
            return;
        }
        if (_alienScene == null)
        {
            GD.PrintErr("Alien scene not assigned.");
            SetProcess(false);
            return;
        }

        //connect to entity death signals
        _player.Died += OnPlayerDied;
        _ship.Died += OnShipDied;

        _gameManager.SceneChanged += OnSceneChanged;

        _gameManager.PushScene("res://Scenes/HUD/HUD.tscn");

        // start
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
        DisconnectEntitySignals();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
    }

    private void StartGame()
    {
        GD.Print("Starting Game");
        _currentState = GameState.Playing;
        _currentRound = 0;
        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        OnRoundTimerTimeout();
        _roundTimer.Start();
    }

    private void EndGame(string reason)
    {
        if (_currentState != GameState.Playing)
            return;
        _currentState = GameState.GameOver;
        _roundTimer?.Stop();
        GD.Print($"Game Over, reason: {reason}. Reached Round: {_currentRound}");
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        EmitSignal(SignalName.GameOver, reason, _currentRound);
        DisconnectEntitySignals();
        _gameManager.ChangeScene(RESULT_SCENE_PATH);
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
    }

    private void OnRoundTimerTimeout()
    {
        if (_currentState != GameState.Playing)
            return;
        _currentRound++;
        GD.Print($"--- Starting Round {_currentRound} ---");
        EmitSignal(SignalName.RoundChanged, _currentRound);
        int aliensToSpawn = InitialAlienCount + (_currentRound - 1) * AlienIncreasePerRound;
        SpawnAlienHorde(aliensToSpawn);
    }

    private void OnPlayerDied() => EndGame("Player Died");

    private void OnShipDied() => EndGame("Ship Destroyed");

    private void OnSceneChanged(string scenePath, bool isPushing)
    {
        if (scenePath == PAUSE_SCENE_PATH)
        {
            bool pausing = isPushing;
            _roundTimer?.SetPaused(pausing);

            ProcessMode = pausing ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            Input.SetMouseMode(
                pausing ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured
            );
            _audioManager?.SetBGMPaused(pausing);
        }
    }

    private void SpawnAlienHorde(int count)
    {
        GD.Print($"Spawning horde of {count} aliens for round {_currentRound}.");
        for (int i = 0; i < count; i++)
            SpawnSingleAlien();
    }

    private void SpawnSingleAlien()
    {
        Alien newAlien = _alienScene.Instantiate<Alien>();
        if (newAlien != null)
        {
            Random random = new Random();
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float distance = 25.0f + (float)(random.NextDouble() * 15.0);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
            Vector3 playerUp = _player.Transform.Basis.Y;
            Vector3 spawnPos = _player.GlobalPosition + offset + playerUp * 2.0f;

            newAlien.GlobalPosition = spawnPos;
            AddChild(newAlien);
        }
        else
            GD.PrintErr("Failed to instantiate Alien scene");
    }

    private T GetNodeFromGroupHelper<T>(string group)
        where T : Node
    {
        var nodes = GetTree().GetNodesInGroup(group);
        if (nodes.Count > 0 && nodes[0] is T typedNode)
            return typedNode;
        return null;
    }
}
