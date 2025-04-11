using Godot;

public partial class Settings : Control
{
	private GameManager _gameManager;
	private SettingsManager _settingsManager;
	private AudioManager _audioManager;

	private HSlider _bgmSlider;
	private HSlider _sfxSlider;
	private Label _bgmValueLabel;
	private CheckButton _hudBobbingToggle;
	private CheckButton _weaponBobbingToggle;
	private Button _backButton;
	private Button _saveButton;

	private float _tempBgmVolume;
	private float _tempSfxVolume;
	private bool _tempHudBobbingEnabled;
	private bool _tempWeaponBobbingEnabled;

	private const string SfxButtonPath = "res://Assets/Audio/button_1.wav";

	// private const string SfxSliderPath = "res://Assets/Audio/slider_1.wav"; future

	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_settingsManager = GetNode<SettingsManager>("/root/SettingsManager");
		_audioManager = GetNode<AudioManager>("/root/AudioManager");

		_bgmSlider = GetNode<HSlider>("SettingsPanel/ContentPanel/AudioSection/BgmSlider");
		_sfxSlider = GetNode<HSlider>("SettingsPanel/ContentPanel/AudioSection/SfxSlider");
		_bgmValueLabel = GetNode<Label>("SettingsPanel/ContentPanel/AudioSection/BgmValue");
		_hudBobbingToggle = GetNode<CheckButton>(
			"SettingsPanel/ContentPanel/GameplaySection/HudBobbingToggle"
		);
		_weaponBobbingToggle = GetNode<CheckButton>(
			"SettingsPanel/ContentPanel/GameplaySection/WeaponBobbingToggle"
		);
		_backButton = GetNode<Button>("SettingsPanel/ButtonContainer/BackButton");
		_saveButton = GetNode<Button>("SettingsPanel/ButtonContainer/SaveButton");

		if (_settingsManager != null)
		{
			_tempBgmVolume = _settingsManager.BgmVolume;
			_tempSfxVolume = _settingsManager.SfxVolume;
			_tempHudBobbingEnabled = _settingsManager.HudBobbingEnabled;
			_tempWeaponBobbingEnabled = _settingsManager.WeaponBobbingEnabled;

			InitializeUI();
		}
		else
		{
			_tempBgmVolume = 0.8f;
			_tempSfxVolume = 0.8f;
			_tempHudBobbingEnabled = true;
			_tempWeaponBobbingEnabled = true;
			InitializeUI();
		}

		_bgmSlider?.Connect("value_changed", Callable.From<double>(_on_bgm_slider_value_changed));
		_sfxSlider?.Connect("value_changed", Callable.From<double>(_on_sfx_slider_value_changed));
		_hudBobbingToggle?.Connect("toggled", Callable.From<bool>(_on_hud_bobbing_toggle_toggled));
		_weaponBobbingToggle?.Connect(
			"toggled",
			Callable.From<bool>(_on_weapon_bobbing_toggle_toggled)
		);
		_backButton?.Connect("pressed", Callable.From(_on_back_button_pressed));
		_saveButton?.Connect("pressed", Callable.From(_on_save_button_pressed));
	}

	private void InitializeUI()
	{
		if (_bgmSlider != null)
		{
			_bgmSlider.Value = _tempBgmVolume;
			UpdateBgmValueLabel(_tempBgmVolume);
		}
		if (_sfxSlider != null)
			_sfxSlider.Value = _tempSfxVolume;

		if (_hudBobbingToggle != null)
		{
			_hudBobbingToggle.ButtonPressed = _tempHudBobbingEnabled;
			UpdateCheckButtonText(_hudBobbingToggle, _tempHudBobbingEnabled);
		}
		if (_weaponBobbingToggle != null)
		{
			_weaponBobbingToggle.ButtonPressed = _tempWeaponBobbingEnabled;
			UpdateCheckButtonText(_weaponBobbingToggle, _tempWeaponBobbingEnabled);
		}
	}

	private void _on_bgm_slider_value_changed(double value)
	{
		_tempBgmVolume = (float)value;
		UpdateBgmValueLabel(_tempBgmVolume);
	}

	private void _on_sfx_slider_value_changed(double value)
	{
		_tempSfxVolume = (float)value;
	}

	private void _on_hud_bobbing_toggle_toggled(bool buttonPressed)
	{
		_tempHudBobbingEnabled = buttonPressed;
		UpdateCheckButtonText(_hudBobbingToggle, buttonPressed);
		_audioManager?.PlaySFX(SfxButtonPath);
	}

	private void _on_weapon_bobbing_toggle_toggled(bool buttonPressed)
	{
		_tempWeaponBobbingEnabled = buttonPressed;
		UpdateCheckButtonText(_weaponBobbingToggle, buttonPressed);
		_audioManager?.PlaySFX(SfxButtonPath);
	}

	private void _on_back_button_pressed()
	{
		_audioManager?.PlaySFX(SfxButtonPath);
		_gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
	}

	private void _on_save_button_pressed()
	{
		_audioManager?.PlaySFX(SfxButtonPath);
		if (_settingsManager != null)
		{
			_settingsManager.BgmVolume = _tempBgmVolume;
			_settingsManager.SfxVolume = _tempSfxVolume;
			_settingsManager.HudBobbingEnabled = _tempHudBobbingEnabled;
			_settingsManager.WeaponBobbingEnabled = _tempWeaponBobbingEnabled;
		}
		_gameManager.ChangeScene("res://Scenes/MainMenu/MainMenu.tscn");
	}

	private void UpdateBgmValueLabel(float value)
	{
		if (_bgmValueLabel != null)
		{
			_bgmValueLabel.Text = $"{value * 100:F0}%";
		}
	}

	private void UpdateCheckButtonText(CheckButton button, bool isEnabled)
	{
		if (button != null)
		{
			button.Text = isEnabled ? "ENABLED" : "DISABLED";
		}
	}
}
