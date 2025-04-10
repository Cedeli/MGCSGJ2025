using System;
using Godot;

public partial class HUD : Control
{
    private Label _timerLabel;
    private Label _roundLabel;
    private Label _scoreLabel;
    private ProgressBar _playerHealthBar;
    private ProgressBar _shipHealthBar;
    private Button _pauseButton;

    // Services
    private GameManager _gameManager;
    private AudioManager _audioManager;

    // Node References
    private Game _gameScene;
    private Player _player;
    private Ship _ship;

    // Constants
    private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";
    private const string PAUSE_SCENE_PATH = "res://Scenes/Pause/Pause.tscn";

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        _timerLabel = GetNodeOrNull<Label>("TopLeftPanel/TopLeftInfo/TimerLabel");
        _roundLabel = GetNodeOrNull<Label>("TopLeftPanel/TopLeftInfo/RoundLabel");
        _scoreLabel = GetNodeOrNull<Label>("TopLeftPanel/TopLeftInfo/ScoreLabel");
        _playerHealthBar = GetNodeOrNull<ProgressBar>(
            "LeftHealthBars/StatusPanel/BarsContainer/PlayerHealthBar"
        );
        _shipHealthBar = GetNodeOrNull<ProgressBar>(
            "LeftHealthBars/StatusPanel/BarsContainer/ShipHealthBar"
        );
        _pauseButton = GetNodeOrNull<Button>("PauseButton");

        if (_pauseButton != null)
            _pauseButton.Pressed += OnPauseButtonPressed;
        else
            GD.PrintErr("HUD Error: PauseButton node not found");

        if (_timerLabel == null)
            GD.PrintErr("HUD Error: TimerLabel node not found");
        if (_roundLabel == null)
            GD.PrintErr("HUD Error: RoundLabel node not found");
        if (_scoreLabel == null)
            GD.PrintErr("HUD Error: ScoreLabel node not found");
        if (_playerHealthBar == null)
            GD.PrintErr("HUD Error: PlayerHealthBar node not found");
        if (_shipHealthBar == null)
            GD.PrintErr("HUD Error: ShipHealthBar node not found");

        CallDeferred(nameof(ConnectToGameSignals));
    }

    private void ConnectToGameSignals()
    {
        _gameScene = _gameManager?.GetGameScene();
        if (_gameScene != null)
        {
            _gameScene.RoundChanged += UpdateRound;
            _gameScene.RoundTimerUpdate += UpdateTimer;
            _gameScene.ScoreUpdated += UpdateScore;
            UpdateRound(_gameScene.GetCurrentRound());
            UpdateScore(_gameScene.GetCurrentScore());
        }
        else
            GD.PrintErr("HUD Error: Could not find Game scene to connect signals");

        _player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
        if (_player != null)
        {
            _player.HealthChanged += UpdatePlayerHealth;
            UpdatePlayerHealth(_player.CurrentHealth, _player.MaxHealth);
        }
        else
            GD.PrintErr("HUD Error: Could not find Player for health signal");

        _ship = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);
        if (_ship != null)
        {
            _ship.HullChanged += UpdateShipHealth;
            UpdateShipHealth(_ship.CurrentHull, _ship.MaxHull);
        }
        else
            GD.PrintErr("HUD Error: Could not find Ship for hull signal");
    }

    public override void _ExitTree()
    {
        if (_gameScene != null && IsInstanceValid(_gameScene))
        {
            if (
                _gameScene.IsConnected(
                    Game.SignalName.RoundChanged,
                    Callable.From<int>(UpdateRound)
                )
            )
                _gameScene.RoundChanged -= UpdateRound;
            if (
                _gameScene.IsConnected(
                    Game.SignalName.RoundTimerUpdate,
                    Callable.From<float>(UpdateTimer)
                )
            )
                _gameScene.RoundTimerUpdate -= UpdateTimer;
            if (
                _gameScene.IsConnected(
                    Game.SignalName.ScoreUpdated,
                    Callable.From<int>(UpdateScore)
                )
            )
                _gameScene.ScoreUpdated -= UpdateScore;
        }
        if (_player != null && IsInstanceValid(_player))
        {
            if (
                _player.IsConnected(
                    Player.SignalName.HealthChanged,
                    Callable.From<float, float>(UpdatePlayerHealth)
                )
            )
                _player.HealthChanged -= UpdatePlayerHealth;
        }
        if (_ship != null && IsInstanceValid(_ship))
        {
            if (
                _ship.IsConnected(
                    Ship.SignalName.HullChanged,
                    Callable.From<float, float>(UpdateShipHealth)
                )
            )
                _ship.HullChanged -= UpdateShipHealth;
        }
    }

    // Callbacks
    public void UpdateTimer(float time)
    {
        if (_timerLabel != null)
            _timerLabel.Text = $"Time: {Math.Max(0, time):F1}s";
    }

    public void UpdateRound(int round)
    {
        if (_roundLabel != null)
            _roundLabel.Text = $"Round: {round}";
    }

    public void UpdateScore(int score)
    {
        if (_scoreLabel != null)
            _scoreLabel.Text = $"Score: {score}";
    }

    public void UpdatePlayerHealth(float currentHealth, float maxHealth)
    {
        if (_playerHealthBar != null)
        {
            if (_playerHealthBar.MaxValue != maxHealth)
                _playerHealthBar.MaxValue = maxHealth;
            _playerHealthBar.Value = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
    }

    public void UpdateShipHealth(float currentHull, float maxHull)
    {
        if (_shipHealthBar != null)
        {
            if (_shipHealthBar.MaxValue != maxHull)
                _shipHealthBar.MaxValue = maxHull;
            _shipHealthBar.Value = Mathf.Clamp(currentHull, 0, maxHull);
        }
    }

    // Internal
    private void OnPauseButtonPressed()
    {
        _audioManager?.PlaySFX(SfxButtonPath);
        _gameManager?.PushScene(PAUSE_SCENE_PATH);
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
