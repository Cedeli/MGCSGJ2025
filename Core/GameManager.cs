using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SceneManager : Node
{
	[Export] private Node sceneContainer;

	private Dictionary<string, PackedScene> _sceneCache = new Dictionary<string, PackedScene>();
	private Stack<Node> _sceneStack = new Stack<Node>();

	public override void _Ready()
	{
		if (sceneContainer == null)
		{
			sceneContainer = this;
			GD.Print($"{Name}: Scene Container not set in editor- defaulting to self.");
		}
	}

	public void PushScene(string scenePath, bool hideAndPausePrevious = true)
	{
		Node currentScene = null;
		if (hideAndPausePrevious && _sceneStack.TryPeek(out currentScene))
		{
			GD.Print($"Pausing scene: {currentScene.Name} ({currentScene.SceneFilePath})");
			currentScene.ProcessMode = ProcessModeEnum.Disabled;
		}

		Node newSceneInstance = LoadAndInstantiateScene(scenePath);
		if (newSceneInstance == null)
		{
			GD.PrintErr($"Failed to push scene: Could not load or instantiate {scenePath}");
			if (hideAndPausePrevious && currentScene != null)
			{
				 currentScene.ProcessMode = ProcessModeEnum.Inherit;
			}
			return;
		}

		GD.Print($"Pushing scene: {newSceneInstance.Name} ({scenePath})");
		_sceneStack.Push(newSceneInstance);
		sceneContainer.AddChild(newSceneInstance);
		newSceneInstance.ProcessMode = ProcessModeEnum.Inherit;
	}

	public void PopScene()
	{
		if (_sceneStack.Count == 0)
		{
			GD.PrintWarn("Scene stack is empty, cannot pop.");
			return;
		}

		Node currentScene = _sceneStack.Pop();
		GD.Print($"Popping scene: {currentScene.Name} ({currentScene.SceneFilePath})");
		sceneContainer.RemoveChild(currentScene);
		currentScene.QueueFree();

		if (_sceneStack.TryPeek(out Node previousScene))
		{
			 GD.Print($"Resuming scene: {previousScene.Name} ({previousScene.SceneFilePath})");
			previousScene.ProcessMode = ProcessModeEnum.Inherit;
		}
	}

	public void ChangeScene(string scenePath)
	{
		GD.Print($"Changing scene to: {scenePath} (clearing stack)");

		while (_sceneStack.Count > 0)
		{
			Node sceneToUnload = _sceneStack.Pop();
			GD.Print($"-- Unloading {sceneToUnload.Name} ({sceneToUnload.SceneFilePath})");
			sceneContainer.RemoveChild(sceneToUnload);
			sceneToUnload.QueueFree();
		}

		PushScene(scenePath, false);
	}

	public Node GetCurrentScene()
	{
		_sceneStack.TryPeek(out Node currentScene);
		return currentScene;
	}

	private PackedScene LoadScene(string scenePath)
	{
		if (_sceneCache.TryGetValue(scenePath, out PackedScene cachedScene))
		{
			GD.Print($"Using cached scene: {scenePath}");
			return cachedScene;
		}
		else
		{
			GD.Print($"Loading scene: {scenePath}");
			var resource = ResourceLoader.Load<PackedScene>(scenePath);
			if (resource == null)
			{
				GD.PrintErr($"Failed to load scene resource at path: {scenePath}");
				return null;
			}

			 GD.Print($"Caching scene: {scenePath}");
			_sceneCache.Add(scenePath, resource);
			return resource;
		}
	}

	private Node LoadAndInstantiateScene(string scenePath)
	{
		PackedScene packedScene = LoadScene(scenePath);
		if (packedScene == null)
		{
			return null;
		}

		Node sceneInstance = packedScene.Instantiate();
		if (sceneInstance == null)
		{
			 GD.PrintErr($"Failed to instantiate scene: {scenePath}");
			 return null;
		}

		sceneInstance.SceneFilePath = scenePath;

		return sceneInstance;
	}
}