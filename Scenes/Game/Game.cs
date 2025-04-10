using System;
using System.Collections.Generic;
using Godot;

public partial class Game : Node3D
{
	[Signal]
	public delegate void RoundChangedEventHandler(int newRound);

	[Signal]
	public delegate void GameOverEventHandler(string reason, int finalRound, int finalScore);

	[Signal]
	public delegate void RoundTimerUpdateEventHandler(float timeRemaining);

	[Signal]
	public delegate void ScoreUpdatedEventHandler(int newScore);

	private enum GameState
	{
		Playing,
		GameOver,
	}

	[ExportGroup("Game Rules")]
	[Export]
	private float RoundDurationSeconds = 30.0f;

	[Export]
	private int InitialAlienCount = 5;

	[Export]
	private int AlienIncreasePerRound = 3;

	[Export]
	private int AlienScoreValue = 10;

	[ExportGroup("Scene References")]
	[Export]
	private PackedScene _alienScene;

	[ExportGroup("Item Spawning")]
	[Export]
	private PackedScene _healthItemScene;

	[Export]
	private PackedScene _ammoItemScene;

	[Export]
	private PackedScene _powerupItemScene;

	[Export(PropertyHint.Range, "0, 10, 1")]
	private int MaxItemsPerRound = 5;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
	private float ItemSpawnChance = 0.85f;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
	private float HealthItemProbability = 0.4f;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
	private float AmmoItemProbability = 0.3f;

	[Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
	private float PowerupItemProbability = 0.3f;

	private GameManager _gameManager;
	private AudioManager _audioManager;

	private Timer _roundTimer;
	private Player _player;
	private Ship _ship;
	private GameState _currentState = GameState.Playing;
	private int _currentRound = 0;
	private int _currentScore = 0;
	private Random _random = new Random();

	private const string PAUSE_SCENE_PATH = "res://Scenes/Pause/Pause.tscn";
	private const string RESULT_SCENE_PATH = "res://Scenes/Result/Result.tscn";

	public int GetCurrentRound() => _currentRound;

	public int GetCurrentScore() => _currentScore;

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		_roundTimer = new Timer
		{
			Name = "RoundTimer",
			WaitTime = RoundDurationSeconds,
			OneShot = false,
		};
		AddChild(_roundTimer);
		_roundTimer.Timeout += OnRoundTimerTimeout;

		_player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
		_ship = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);

		if (_player == null)
			GD.PrintErr("Player not found");
		if (_ship == null)
			GD.PrintErr("Ship not found");
		if (_alienScene == null)
			GD.PrintErr("Alien scene not assigned");
		if (_healthItemScene == null)
			GD.PrintErr("HealthItem scene not assigned");
		if (_ammoItemScene == null)
			GD.PrintErr("AmmoItem scene not assigned");
		if (_powerupItemScene == null)
			GD.PrintErr("PowerupItem scene not assigned");

		float totalProb = HealthItemProbability + AmmoItemProbability + PowerupItemProbability;
		if (Math.Abs(totalProb - 1.0f) > 0.01f)
		{
			GD.Print($"Item spawn probabilities sum to {totalProb} should be close to 1.0");
		}

		if (_player != null)
			_player.Died += OnPlayerDied;
		if (_ship != null)
			_ship.Died += OnShipDied;
		_gameManager.SceneChanged += OnSceneChanged;
		_gameManager.PushScene("res://Scenes/HUD/HUD.tscn");

		StartGame();
	}

	public override void _Process(double delta)
	{
		if (_currentState != GameState.Playing)
			return;
		EmitSignal(SignalName.RoundTimerUpdate, (float)(_roundTimer?.TimeLeft ?? 0));
	}

	public override void _ExitTree()
	{
		if (_gameManager != null)
			_gameManager.SceneChanged -= OnSceneChanged;
		if (IsInstanceValid(_roundTimer))
			_roundTimer.Timeout -= OnRoundTimerTimeout;
		DisconnectEntitySignals();
		Input.SetMouseMode(Input.MouseModeEnum.Visible);
	}

	private void StartGame()
	{
		_currentState = GameState.Playing;
		_currentRound = 0;
		_currentScore = 0;
		EmitSignal(SignalName.ScoreUpdated, _currentScore);
		Input.SetMouseMode(Input.MouseModeEnum.Captured);
		OnRoundTimerTimeout();
		_roundTimer.Start();
	}

	private void EndGame(string reason)
	{
		if (_currentState != GameState.Playing)
			return;

		_currentState = GameState.GameOver;
		_roundTimer?.Stop();
		Input.SetMouseMode(Input.MouseModeEnum.Visible);
		EmitSignal(SignalName.GameOver, reason, _currentRound, _currentScore);
		DisconnectEntitySignals();
		ClearSpawnedEntities("alien");
		ClearSpawnedEntities("item");

		_gameManager.SetLastGameScore(_currentScore);
		_gameManager.ChangeScene(RESULT_SCENE_PATH);
	}

	private void AddScore(int amount)
	{
		if (_currentState != GameState.Playing)
			return;
		_currentScore += amount;
		EmitSignal(SignalName.ScoreUpdated, _currentScore);
	}

	private void OnAlienDied()
	{
		AddScore(AlienScoreValue);
	}

	private void DisconnectEntitySignals()
	{
		if (_player != null && IsInstanceValid(_player))
		{
			if (_player.IsConnected(Player.SignalName.Died, Callable.From(OnPlayerDied)))
				_player.Died -= OnPlayerDied;
		}
		if (_ship != null && IsInstanceValid(_ship))
		{
			if (_ship.IsConnected(Ship.SignalName.Died, Callable.From(OnShipDied)))
				_ship.Died -= OnShipDied;
		}
		ClearSignalsFromGroup("alien", Alien.SignalName.Died, Callable.From(OnAlienDied));
	}

	private void OnRoundTimerTimeout()
	{
		if (_currentState != GameState.Playing)
			return;
		_currentRound++;
		EmitSignal(SignalName.RoundChanged, _currentRound);

		int aliensToSpawn = InitialAlienCount + (_currentRound - 1) * AlienIncreasePerRound;
		SpawnAlienHorde(aliensToSpawn);
		SpawnItems();
	}

	private void OnPlayerDied() => EndGame("Player Died");

	private void OnShipDied() => EndGame("Ship Destroyed");

	private void OnSceneChanged(string scenePath, bool isPushing)
	{
		if (scenePath == PAUSE_SCENE_PATH)
		{
			bool pausing = isPushing;
			_roundTimer?.SetPaused(pausing);
			ProcessMode = pausing ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
			Input.SetMouseMode(
				pausing ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured
			);
			_audioManager?.SetBGMPaused(pausing);
		}
	}

	private void SpawnAlienHorde(int count)
	{
		for (int i = 0; i < count; i++)
			SpawnSingleAlien();
	}

	private void SpawnSingleAlien()
	{
		if (_player == null)
			return;

		Alien newAlien = _alienScene.Instantiate<Alien>();
		if (newAlien != null)
		{
			newAlien.Died += OnAlienDied;
			newAlien.AddToGroup("alien");

			float angle = (float)(_random.NextDouble() * Math.PI * 2);
			float distance = 30.0f + (float)(_random.NextDouble() * 20.0);
			Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
			Vector3 playerUp = _player.Transform.Basis.Y;
			Vector3 spawnPos = _player.GlobalPosition + offset + playerUp * 3.0f;

			newAlien.GlobalPosition = spawnPos;
			AddChild(newAlien);
		}
	}

	private void SpawnItems()
	{
		int itemsToAttempt = _random.Next(1, MaxItemsPerRound + 1);
		for (int i = 0; i < itemsToAttempt; i++)
		{
			if (_random.NextDouble() < ItemSpawnChance)
			{
				SpawnSingleItem();
			}
		}
	}

	private void SpawnSingleItem()
	{
		if (_player == null)
			return;

		PackedScene itemSceneToSpawn = ChooseItemScene();
		if (itemSceneToSpawn == null)
			return;

		Item newItem = itemSceneToSpawn.Instantiate<Item>();
		if (newItem != null)
		{
			newItem.AddToGroup("item");

			float angle = (float)(_random.NextDouble() * Math.PI * 2);
			float distance = 8.0f + (float)(_random.NextDouble() * 7.0);
			Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
			Vector3 playerUp = _player.Transform.Basis.Y;
			Vector3 spawnPos = _player.GlobalPosition + offset + playerUp * 1.0f;

			newItem.GlobalPosition = spawnPos;
			AddChild(newItem);
		}
	}

	private PackedScene ChooseItemScene()
	{
		double roll = _random.NextDouble();
		float cumulativeProbability = 0f;

		cumulativeProbability += HealthItemProbability;
		if (roll < cumulativeProbability && _healthItemScene != null)
			return _healthItemScene;

		cumulativeProbability += AmmoItemProbability;
		if (roll < cumulativeProbability && _ammoItemScene != null)
			return _ammoItemScene;

		cumulativeProbability += PowerupItemProbability; // Add powerup probability
		if (roll < cumulativeProbability && _powerupItemScene != null)
			return _powerupItemScene;

		return _healthItemScene ?? _ammoItemScene ?? _powerupItemScene;
	}

	private T GetNodeFromGroupHelper<T>(string group)
		where T : Node
	{
		var nodes = GetTree().GetNodesInGroup(group);
		if (nodes.Count > 0 && nodes[0] is T typedNode)
			return typedNode;
		return null;
	}

	private void ClearSpawnedEntities(string groupName)
	{
		foreach (Node node in GetTree().GetNodesInGroup(groupName))
		{
			if (node is Alien alien)
			{
				if (alien.IsConnected(Alien.SignalName.Died, Callable.From(OnAlienDied)))
				{
					alien.Died -= OnAlienDied;
				}
			}
			node.QueueFree();
		}
	}

	private void ClearSignalsFromGroup(string groupName, StringName signalName, Callable callable)
	{
		foreach (Node node in GetTree().GetNodesInGroup(groupName))
		{
			if (node.IsConnected(signalName, callable))
			{
				node.Disconnect(signalName, callable);
			}
		}
	}
}
