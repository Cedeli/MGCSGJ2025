using System.Threading.Tasks;
using Godot;

public partial class MainMenu : Control
{
    private GameManager _gameManager;
    private AudioManager _audioManager;

    private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

    [ExportGroup("Components")]
    [Export]
    protected Button StartButton;

    [Export]
    protected Button CreditsButton;

    [Export]
    protected Button QuitButton;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        StartButton.Pressed += OnStartButtonPressed;
        CreditsButton.Pressed += OnCreditsButtonPressed;
        QuitButton.Pressed += OnQuitButtonPressed;
    }

    private async void OnStartButtonPressed()
    {
        _audioManager.PlaySFX(SfxButtonPath);
        StartButton.Disabled = true;
        CreditsButton.Disabled = true;
        QuitButton.Disabled = true;

        _gameManager.PushScene("res://Scenes/Loading/Loading.tscn");
        await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);
        _gameManager.ChangeScene("res://Scenes/Game/Game.tscn");
    }

    private void OnCreditsButtonPressed()
    {
        _audioManager.PlaySFX(SfxButtonPath);
        _gameManager.ChangeScene("res://Scenes/Credits/Credits.tscn");
    }

    private void OnQuitButtonPressed()
    {
        _audioManager.PlaySFX(SfxButtonPath);
        GetTree().Quit();
    }
}
