using Godot;

public partial class HUD : Control
{
    private GameManager _gameManager;
    private AudioManager _audioManager;
    private Button _pauseButton;

    private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");
        _pauseButton = GetNode<Button>("PauseButton");
        _pauseButton.Pressed += OnPauseButtonPressed;
    }

    private void OnPauseButtonPressed()
    {
        _audioManager.PlaySFX(SfxButtonPath);
        _gameManager.PushScene("res://Scenes/Pause/Pause.tscn");
    }
}
