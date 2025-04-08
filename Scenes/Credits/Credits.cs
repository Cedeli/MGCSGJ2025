using Godot;

public partial class Credits : Control
{
    private GameManager _gameManager;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        
        var backButton = GetNode<Button>("VBoxContainer/BackButton");
        backButton.Pressed += OnBackButtonPressed;
    }

    private void OnBackButtonPressed()
    {
        _gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
    }
} 