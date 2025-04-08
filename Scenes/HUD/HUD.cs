using Godot;

public partial class HUD : Control
{
	private GameManager _gameManager;
	private Button _pauseButton;

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_pauseButton = GetNode<Button>("PauseButton");
		_pauseButton.Pressed += OnPauseButtonPressed;
	}

	private void OnPauseButtonPressed()
	{
		_gameManager.PushScene("res://Scenes/Pause/Pause.tscn");
	}
} 
