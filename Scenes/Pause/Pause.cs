using Godot;

public partial class Pause : Control
{
	private GameManager _gameManager;
	private AudioManager _audioManager;
	private Button _resumeButton;
	private Button _quitButton;
	private Panel _overlay;

	private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_gameManager = GetNode<GameManager>("/root/GameManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		_resumeButton = GetNode<Button>("Panel/VBoxContainer/ResumeButton");
		_quitButton = GetNode<Button>("Panel/VBoxContainer/QuitButton");

		_resumeButton.Pressed += OnResumeButtonPressed;
		_quitButton.Pressed += OnQuitButtonPressed;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("pause"))
		{
			GetViewport().SetInputAsHandled();
			OnResumeButtonPressed();
		}
	}

	private void OnResumeButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
		_gameManager.PopScene();
	}

	private void OnQuitButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
		_gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
	}
}
