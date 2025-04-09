using Godot;

public partial class Main : Node3D
{
    private GameManager _gameManager;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
    }
}
