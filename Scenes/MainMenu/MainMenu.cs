using Godot;

public partial class MainMenu : Control
{
	private GameManager _gameManager;
	private AudioManager _audioManager;

	private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		var startButton = GetNode<Button>("Panel/VBoxContainer/StartButton");
		var creditsButton = GetNode<Button>("Panel/VBoxContainer/CreditsButton");
		var quitButton = GetNode<Button>("Panel/VBoxContainer/QuitButton");

		startButton.Pressed += OnStartButtonPressed;
		creditsButton.Pressed += OnCreditsButtonPressed;
		quitButton.Pressed += OnQuitButtonPressed;
	}

	private void OnStartButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
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
