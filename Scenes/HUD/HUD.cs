using System;
using Godot;

public partial class HUD : Control
{
    private Label _timerLabel;
    private Label _roundLabel;
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
        _playerHealthBar = GetNodeOrNull<ProgressBar>(
            "LeftHealthBars/StatusPanel/BarsContainer/PlayerHealthBar"
        );
        _shipHealthBar = GetNodeOrNull<ProgressBar>(
            "LeftHealthBars/StatusPanel/BarsContainer/ShipHealthBar"
        );
        _pauseButton = GetNodeOrNull<Button>("PauseButton");

        if (_pauseButton != null)
        {
            _pauseButton.Pressed += OnPauseButtonPressed;
        }
        else
            GD.PrintErr("HUD Error: PauseButton node not found!");

        // avoid errors in the future
        if (_timerLabel == null)
            GD.PrintErr(
                "HUD Error: TimerLabel node not found at 'TopLeftPanel/TopLeftInfo/TimerLabel'"
            );
        if (_roundLabel == null)
            GD.PrintErr(
                "HUD Error: RoundLabel node not found at 'TopLeftPanel/TopLeftInfo/RoundLabel'"
            );
        if (_playerHealthBar == null)
            GD.PrintErr(
                "HUD Error: PlayerHealthBar node not found at 'LeftHealthBars/StatusPanel/BarsContainer/PlayerHealthBar'"
            );
        if (_shipHealthBar == null)
            GD.PrintErr(
                "HUD Error: ShipHealthBar node not found at 'LeftHealthBars/StatusPanel/BarsContainer/ShipHealthBar'"
            );

        CallDeferred(nameof(ConnectToGameSignals));
    }

    private void ConnectToGameSignals()
    {
        _gameScene = _gameManager?.GetGameScene();
        if (_gameScene != null)
        {
            _gameScene.RoundChanged += UpdateRound;
            _gameScene.RoundTimerUpdate += UpdateTimer;
            UpdateRound(_gameScene.GetCurrentRound()); // sync
        }
        else
            GD.PrintErr("HUD Error: Could not find Game scene to connect signals");

        // Find Player via group
        _player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
        if (_player != null)
        {
            _player.HealthChanged += UpdatePlayerHealth;
            UpdatePlayerHealth(_player.CurrentHealth, _player.MaxHealth); //sync
        }
        else
            GD.PrintErr("HUD Error: Could not find Player for health signal");

        // Find Ship via group
        _ship = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);
        if (_ship != null)
        {
            _ship.HullChanged += UpdateShipHealth;
            UpdateShipHealth(_ship.CurrentHull, _ship.MaxHull); // sync
        }
        else
            GD.PrintErr("HUD Error: Could not find Ship for hull signal");
    }

    public override void _ExitTree()
    {
        // Future implementation
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
