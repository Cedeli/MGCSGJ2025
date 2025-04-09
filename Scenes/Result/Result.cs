using Godot;

public partial class Result : Control
{
	private GameManager _gameManager;

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		
		var mainMenuButton = GetNode<Button>("Panel/VBoxContainer/HBoxContainer/MainMenuButton");
		var restartButton = GetNode<Button>("Panel/VBoxContainer/HBoxContainer/RestartButton");

		mainMenuButton.Pressed += OnMainMenuButtonPressed;
		restartButton.Pressed += OnRestartButtonPressed;
	}

	private void OnMainMenuButtonPressed()
	{
		_gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
	}

	private void OnRestartButtonPressed()
	{
		_gameManager.ChangeScene("res://Scenes/Game/Game.tscn");
	}
} 
