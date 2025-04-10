using Godot;

public partial class Result : Control
{
	private GameManager _gameManager;
	private AudioManager _audioManager;
	private Label _resultTextLabel;

	private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		var mainMenuButton = GetNode<Button>("Panel/VBoxContainer/HBoxContainer/MainMenuButton");
		var restartButton = GetNode<Button>("Panel/VBoxContainer/HBoxContainer/RestartButton");
		_resultTextLabel = GetNode<Label>("Panel/VBoxContainer/ResultText"); // Get the label

		mainMenuButton.Pressed += OnMainMenuButtonPressed;
		restartButton.Pressed += OnRestartButtonPressed;

		int finalScore = _gameManager.GetLastGameScore();
		if (_resultTextLabel != null)
		{
			_resultTextLabel.Text = $"Your Score: {finalScore}";
		}
		else
		{
			GD.PrintErr("could not find resultText label");
		}
	}

	private void OnMainMenuButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
		_gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
	}

	private void OnRestartButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
		_gameManager.ChangeScene("res://Scenes/Game/Game.tscn");
	}
}
