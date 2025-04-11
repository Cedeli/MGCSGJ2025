using System;
using Godot;

public partial class HUD : Control
{
	private Label _timerLabel;
	private Label _roundLabel;
	private Label _scoreLabel;
	private ProgressBar _playerHealthBar;
	private ProgressBar _shipHealthBar;
	private Label _playerHealthValueLabel;
	private Label _shipHealthValueLabel;
	private Button _pauseButton;

	private Label _ammoCountLabel;

	private GameManager _gameManager;
	private AudioManager _audioManager;

	private Game _gameScene;
	private Player _player;
	private Ship _ship;
	private Gun _playerGun;

	private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";
	private const string PAUSE_SCENE_PATH = "res://Scenes/Pause/Pause.tscn";

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		_timerLabel = GetNodeOrNull<Label>("TopLeftPanel/TopLeftInfo/TimerContainer/TimerLabel");
		_roundLabel = GetNodeOrNull<Label>("TopLeftPanel/TopLeftInfo/RoundContainer/RoundLabel");
		_scoreLabel = GetNodeOrNull<Label>("TopLeftPanel/TopLeftInfo/ScoreContainer/ScoreLabel");
		_playerHealthBar = GetNodeOrNull<ProgressBar>(
			"LeftHealthBars/StatusPanel/BarsContainer/PlayerHealthContainer/PlayerHealthBar"
		);
		_playerHealthValueLabel = GetNodeOrNull<Label>(
			"LeftHealthBars/StatusPanel/BarsContainer/PlayerHealthContainer/PlayerHeader/PlayerHealthValue"
		);
		_shipHealthBar = GetNodeOrNull<ProgressBar>(
			"LeftHealthBars/StatusPanel/BarsContainer/ShipHealthContainer/ShipHealthBar"
		);
		_shipHealthValueLabel = GetNodeOrNull<Label>(
			"LeftHealthBars/StatusPanel/BarsContainer/ShipHealthContainer/ShipHeader/ShipHealthValue"
		);
		_pauseButton = GetNodeOrNull<Button>("PauseButton");

		_ammoCountLabel = GetNodeOrNull<Label>("AmmoPanel/AmmoContainer/AmmoCount");

		if (_pauseButton != null)
			_pauseButton.Pressed += OnPauseButtonPressed;

		CallDeferred(nameof(ConnectToGameSignals));
	}

	private void ConnectToGameSignals()
	{
		_gameScene = _gameManager?.GetGameScene();
		if (_gameScene != null)
		{
			if (
				!_gameScene.IsConnected(
					Game.SignalName.RoundChanged,
					Callable.From<int>(UpdateRound)
				)
			)
				_gameScene.RoundChanged += UpdateRound;
			if (
				!_gameScene.IsConnected(
					Game.SignalName.RoundTimerUpdate,
					Callable.From<float>(UpdateTimer)
				)
			)
				_gameScene.RoundTimerUpdate += UpdateTimer;
			if (
				!_gameScene.IsConnected(
					Game.SignalName.ScoreUpdated,
					Callable.From<int>(UpdateScore)
				)
			)
				_gameScene.ScoreUpdated += UpdateScore;

			UpdateRound(_gameScene.GetCurrentRound());
			UpdateScore(_gameScene.GetCurrentScore());
		}
		else
			GD.PrintErr("HUD Error:could not find Game scene to connect signals");

		_player = GetNodeFromGroupHelper<Player>(Player.PlayerGroup);
		if (_player != null)
		{
			if (
				!_player.IsConnected(
					Player.SignalName.HealthChanged,
					Callable.From<float, float>(UpdatePlayerHealth)
				)
			)
				_player.HealthChanged += UpdatePlayerHealth;
			UpdatePlayerHealth(_player.CurrentHealth, _player.MaxHealth);

			_playerGun = _player.GetGun();
			if (_playerGun != null)
			{
				if (
					!_playerGun.IsConnected(
						Gun.SignalName.AmmoChanged,
						Callable.From<int, int>(UpdateAmmoCount)
					)
				)
				{
					_playerGun.AmmoChanged += UpdateAmmoCount;
				}
			}
			else
				GD.PrintErr("HUD Error: could not find Player Gun for ammo signal");
		}
		else
			GD.PrintErr("HUD Error: could not find Player for health/gun signals");

		_ship = GetNodeFromGroupHelper<Ship>(Ship.ShipGroup);
		if (_ship != null)
		{
			if (
				!_ship.IsConnected(
					Ship.SignalName.HullChanged,
					Callable.From<float, float>(UpdateShipHealth)
				)
			)
				_ship.HullChanged += UpdateShipHealth;
			UpdateShipHealth(_ship.CurrentHull, _ship.MaxHull);
		}
		else
			GD.PrintErr("HUD Error:could not find Ship for hull signal");
	}

	public override void _ExitTree()
	{
		if (_gameScene != null && IsInstanceValid(_gameScene))
		{
			if (
				_gameScene.IsConnected(
					Game.SignalName.RoundChanged,
					Callable.From<int>(UpdateRound)
				)
			)
				_gameScene.RoundChanged -= UpdateRound;
			if (
				_gameScene.IsConnected(
					Game.SignalName.RoundTimerUpdate,
					Callable.From<float>(UpdateTimer)
				)
			)
				_gameScene.RoundTimerUpdate -= UpdateTimer;
			if (
				_gameScene.IsConnected(
					Game.SignalName.ScoreUpdated,
					Callable.From<int>(UpdateScore)
				)
			)
				_gameScene.ScoreUpdated -= UpdateScore;
		}
		if (_player != null && IsInstanceValid(_player))
		{
			if (
				_player.IsConnected(
					Player.SignalName.HealthChanged,
					Callable.From<float, float>(UpdatePlayerHealth)
				)
			)
				_player.HealthChanged -= UpdatePlayerHealth;
		}
		if (_playerGun != null && IsInstanceValid(_playerGun))
		{
			if (
				_playerGun.IsConnected(
					Gun.SignalName.AmmoChanged,
					Callable.From<int, int>(UpdateAmmoCount)
				)
			)
				_playerGun.AmmoChanged -= UpdateAmmoCount;
		}
		if (_ship != null && IsInstanceValid(_ship))
		{
			if (
				_ship.IsConnected(
					Ship.SignalName.HullChanged,
					Callable.From<float, float>(UpdateShipHealth)
				)
			)
				_ship.HullChanged -= UpdateShipHealth;
		}
	}

	public void UpdateTimer(float time)
	{
		if (_timerLabel != null)
			_timerLabel.Text = $"TIME: {Math.Max(0, time):F1}s";
	}

	public void UpdateRound(int round)
	{
		if (_roundLabel != null)
			_roundLabel.Text = $"ROUND: {round}";
	}

	public void UpdateScore(int score)
	{
		if (_scoreLabel != null)
			_scoreLabel.Text = $"SCORE: {score}";
	}

	public void UpdatePlayerHealth(float currentHealth, float maxHealth)
	{
		if (maxHealth <= 0)
			return;
		float percentage = Mathf.Clamp(currentHealth / maxHealth, 0f, 1f);

		if (_playerHealthBar != null)
		{
			if (_playerHealthBar.MaxValue != maxHealth)
				_playerHealthBar.MaxValue = maxHealth;
			_playerHealthBar.Value = currentHealth;
			var gradient = _playerHealthBar.GetNodeOrNull<ColorRect>("PlayerHealthGradient");
			var shine = _playerHealthBar.GetNodeOrNull<ColorRect>("PlayerHealthShine");
			if (gradient != null)
				gradient.Scale = new Vector2(percentage, 1f);
			if (shine != null)
				shine.Scale = new Vector2(percentage, 1f);
		}
		if (_playerHealthValueLabel != null)
		{
			_playerHealthValueLabel.Text = $"{percentage:P0}";
		}
	}

	public void UpdateShipHealth(float currentHull, float maxHull)
	{
		if (maxHull <= 0)
			return;
		float percentage = Mathf.Clamp(currentHull / maxHull, 0f, 1f);

		if (_shipHealthBar != null)
		{
			if (_shipHealthBar.MaxValue != maxHull)
				_shipHealthBar.MaxValue = maxHull;
			_shipHealthBar.Value = currentHull;
			var gradient = _shipHealthBar.GetNodeOrNull<ColorRect>("ShipHealthGradient");
			var shine = _shipHealthBar.GetNodeOrNull<ColorRect>("ShipHealthShine");
			if (gradient != null)
				gradient.Scale = new Vector2(percentage, 1f);
			if (shine != null)
				shine.Scale = new Vector2(percentage, 1f);
		}
		if (_shipHealthValueLabel != null)
		{
			_shipHealthValueLabel.Text = $"{percentage:P0}";
		}
	}

	public void UpdateAmmoCount(int currentClip, int currentReserve)
	{
		if (_ammoCountLabel != null)
		{
			_ammoCountLabel.Text = $"{currentClip} / {currentReserve}";
		}
	}

	private void OnPauseButtonPressed()
	{
		_audioManager?.PlaySFX(SfxButtonPath);
		_gameManager?.PushScene(PAUSE_SCENE_PATH);
	}

	private T GetNodeFromGroupHelper<T>(string group)
		where T : Node
	{
		var nodes = GetTree().GetNodesInGroup(group);
		if (nodes.Count > 0 && nodes[0] is T typedNode && IsInstanceValid(typedNode))
			return typedNode;
		return null;
	}
}
