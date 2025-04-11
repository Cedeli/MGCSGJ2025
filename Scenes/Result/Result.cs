using System.Collections.Generic;
using Godot;

public partial class Result : Control
{
	private GameManager _gameManager;
	private AudioManager _audioManager;

	[ExportGroup("Components")]
	[Export]
	protected Button MainMenuButton;

	[Export]
	protected Button RestartButton;

	[Export]
	protected Label ResultTextLabel;

	[Export]
	protected Label RoundValueLabel;

	[Export]
	protected Label ReasonValueLabel;

	[Export]
	protected Label TimeValueLabel;

	[Export]
	protected Label EnemiesValueLabel;

	private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		MainMenuButton.Pressed += OnMainMenuButtonPressed;
		RestartButton.Pressed += OnRestartButtonPressed;

		var parameters = _gameManager.GetParameters();

		UpdateResultUI(parameters);
	}

	private void UpdateResultUI(Dictionary<string, string> parameters)
	{
		int finalScore = 0;
		int finalRound = 0;
		string reason = "Unknown";
		string timeSurvived = "N/A";
		string enemiesDefeated = "N/A";

		if (
			parameters.TryGetValue("finalScore", out string scoreString)
			&& int.TryParse(scoreString, out int score)
		)
		{
			finalScore = score;
		}
		else
		{
			GD.PrintErr("Result Scene Warning: cant parse 'finalScore' parameter");
		}

		if (
			parameters.TryGetValue("finalRound", out string roundString)
			&& int.TryParse(roundString, out int round)
		)
		{
			finalRound = round;
		}
		else
		{
			GD.PrintErr("Result Scene Warning: cant parse 'finalRound' parameter");
		}

		if (parameters.TryGetValue("reason", out string reasonString))
		{
			reason = reasonString;
		}
		else
		{
			GD.PrintErr("Result Scene Warning: cant parse 'reason' parameter");
		}

		if (parameters.TryGetValue("timeSurvived", out string timeString))
		{
			timeSurvived = timeString;
		}
		if (parameters.TryGetValue("enemiesKilled", out string enemiesString))
		{
			enemiesDefeated = enemiesString;
		}

		ResultTextLabel.Text = $"YOUR SCORE: {finalScore}";
		RoundValueLabel.Text = finalRound.ToString();
		ReasonValueLabel.Text = reason;
		TimeValueLabel.Text = timeSurvived;
		EnemiesValueLabel.Text = enemiesDefeated;

		Label resultMessageLabel = GetNodeOrNull<Label>(
			"ResultPanel/ContentContainer/ScorePanel/ScoreInfo/ResultMessage"
		);
		Label gameOverHeaderLabel = GetNodeOrNull<Label>("GameOverHeader");

		if (resultMessageLabel != null && gameOverHeaderLabel != null)
		{
			if (reason.Contains("Died") || reason.Contains("Destroyed"))
			{
				resultMessageLabel.Text = "MISSION FAILED";
				resultMessageLabel.AddThemeColorOverride("font_color", Colors.Red);
				gameOverHeaderLabel.Text = "GAME OVER";
			}
			else
			{
				resultMessageLabel.Text = "MISSION SUCCESSFUL!";
				resultMessageLabel.AddThemeColorOverride("font_color", Colors.LimeGreen);
				gameOverHeaderLabel.Text = "MISSION COMPLETE";
			}
		}
		else { }
	}

	private void OnMainMenuButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
		_gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
	}

	private void OnRestartButtonPressed()
	{
		_audioManager.PlaySFX(SfxButtonPath);
		_gameManager.ChangeScene("res://Scenes/Game/Game.tscn");
	}
}
