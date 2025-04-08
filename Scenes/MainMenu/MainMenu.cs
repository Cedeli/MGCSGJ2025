using Godot;

public partial class MainMenu : Control
{
	private GameManager _gameManager;

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		
		var startButton = GetNode<Button>("Panel/VBoxContainer/StartButton");
		var creditsButton = GetNode<Button>("Panel/VBoxContainer/CreditsButton");
		var quitButton = GetNode<Button>("Panel/VBoxContainer/QuitButton");

		startButton.Pressed += OnStartButtonPressed;
		creditsButton.Pressed += OnCreditsButtonPressed;
		quitButton.Pressed += OnQuitButtonPressed;
	}

	private void OnStartButtonPressed()
	{
		_gameManager.ChangeScene("res://Scenes/Game/Game.tscn");
	}

	private void OnCreditsButtonPressed()
	{
		_gameManager.ChangeScene("res://Scenes/Credits/Credits.tscn");
	}

	private void OnQuitButtonPressed()
	{
		GetTree().Quit();
	}
} 
