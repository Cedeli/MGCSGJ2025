using Godot;

public partial class Credits : Control
{
    private GameManager _gameManager;
    private AudioManager _audioManager;

    [ExportGroup("Components")]
    [Export]
    protected Button BackButton;

    private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        BackButton.Pressed += OnBackButtonPressed;
    }

    private void OnBackButtonPressed()
    {
        _audioManager.PlaySFX(SfxButtonPath);
        _gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
    }
}
