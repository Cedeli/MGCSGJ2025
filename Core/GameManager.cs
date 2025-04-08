using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
	[Signal]
	public delegate void SceneChangedEventHandler(string scenePath, bool isPushing);

	[Export] private Node sceneContainer;

	private Dictionary<string, PackedScene> _sceneCache = new Dictionary<string, PackedScene>();
	private Stack<Node> _sceneStack = new Stack<Node>();

	public override void _Ready()
	{
		if (sceneContainer == null)
		{
			var container = new Control();
			container.Name = "SceneContainer";
			container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			AddChild(container);
			sceneContainer = container;
			GD.Print($"{Name}: Created new scene container.");
		}
	}

	public void PushScene(string scenePath, bool hideAndPausePrevious = true)
	{
		Node currentScene = null;
		if (hideAndPausePrevious && _sceneStack.TryPeek(out currentScene))
		{
			GD.Print($"Pausing scene: {currentScene.Name} ({currentScene.SceneFilePath})");
			SetSceneVisibility(currentScene, false);
			currentScene.ProcessMode = ProcessModeEnum.Disabled;
		}

		Node newSceneInstance = LoadAndInstantiateScene(scenePath);
		if (newSceneInstance == null)
		{
			GD.PrintErr($"Failed to push scene: Could not load or instantiate {scenePath}");
			if (hideAndPausePrevious && currentScene != null)
			{
				SetSceneVisibility(currentScene, true);
				currentScene.ProcessMode = ProcessModeEnum.Inherit;
			}
			return;
		}

		GD.Print($"Pushing scene: {newSceneInstance.Name} ({scenePath})");
		_sceneStack.Push(newSceneInstance);
		sceneContainer.AddChild(newSceneInstance);
		
		newSceneInstance.ProcessMode = ProcessModeEnum.Inherit;
		SetSceneVisibility(newSceneInstance, true);

		EmitSignal(SignalName.SceneChanged, scenePath, true);
	}

	public void PopScene()
	{
		if (_sceneStack.Count == 0)
		{
			GD.Print("Scene stack is empty, cannot pop.");
			return;
		}

		Node currentScene = _sceneStack.Pop();
		GD.Print($"Popping scene: {currentScene.Name} ({currentScene.SceneFilePath})");
		SetSceneVisibility(currentScene, false);
		sceneContainer.RemoveChild(currentScene);
		currentScene.QueueFree();

		if (_sceneStack.TryPeek(out Node previousScene))
		{
			GD.Print($"Resuming scene: {previousScene.Name} ({previousScene.SceneFilePath})");
			SetSceneVisibility(previousScene, true);
			previousScene.ProcessMode = ProcessModeEnum.Inherit;
		}

		EmitSignal(SignalName.SceneChanged, currentScene.SceneFilePath, false);
	}

	public void ChangeScene(string scenePath)
	{
		GD.Print($"Changing scene to: {scenePath} (clearing stack)");

		while (_sceneStack.Count > 0)
		{
			Node sceneToUnload = _sceneStack.Pop();
			GD.Print($"-- Unloading {sceneToUnload.Name} ({sceneToUnload.SceneFilePath})");
			SetSceneVisibility(sceneToUnload, false);
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

	private void SetSceneVisibility(Node scene, bool visible)
	{
		if (scene is CanvasItem canvasItem)
		{
			canvasItem.Visible = visible;
		}
		else if (scene is Node3D node3D)
		{
			node3D.Visible = visible;
		}
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
