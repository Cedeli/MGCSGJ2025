using Godot;

public partial class Main : Node3D
{
    private GameManager _gameManager;
    private AudioManager _audioManager;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        string bgmPath = "res://Assets/Audio/menu.mp3";
        _audioManager.PlayBGM(bgmPath);

        _gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
    }
}
