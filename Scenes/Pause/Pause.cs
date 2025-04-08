using Godot;

public partial class Pause : Control
{
	private GameManager _gameManager;
	private Button _resumeButton;
	private Button _quitButton;
	private Panel _overlay;

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_resumeButton = GetNode<Button>("VBoxContainer/ResumeButton");
		_quitButton = GetNode<Button>("VBoxContainer/QuitButton");

		_resumeButton.Pressed += OnResumeButtonPressed;
		_quitButton.Pressed += OnQuitButtonPressed;
	}

	private void OnResumeButtonPressed()
	{
		_gameManager.PopScene();
	}

	private void OnQuitButtonPressed()
	{
		_gameManager.ChangeScene("res://Scenes/Result/Result.tscn");
	}
} 
