using Godot;

public partial class Game : Node3D
{
	private GameManager _gameManager;
	private const string PAUSE_SCENE_PATH = "res://Scenes/Pause/Pause.tscn";

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_gameManager.PushScene("res://Scenes/HUD/HUD.tscn");
		_gameManager.SceneChanged += OnSceneChanged;
	}

	private void OnSceneChanged(string scenePath, bool isPushing)
	{
		if (scenePath == PAUSE_SCENE_PATH)
		{
			if (isPushing)
			{
				// pause game when pause screen is pushed
				ProcessMode = ProcessModeEnum.Disabled;
			}
			else
			{
				// resume game when pause screen is popped
				ProcessMode = ProcessModeEnum.Inherit;
			}
		}
	}

	public override void _ExitTree()
	{
		if (_gameManager != null)
		{
			_gameManager.SceneChanged -= OnSceneChanged;
		}
	}
} 
