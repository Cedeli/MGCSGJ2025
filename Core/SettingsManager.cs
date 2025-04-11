using Godot;

public partial class SettingsManager : Node
{
    [Signal]
    public delegate void BgmVolumeChangedEventHandler(float linearVolume);

    [Signal]
    public delegate void SfxVolumeChangedEventHandler(float linearVolume);

    [Signal]
    public delegate void HudBobbingToggledEventHandler(bool enabled);

    [Signal]
    public delegate void WeaponBobbingToggledEventHandler(bool enabled);

    private float _bgmVolume = 0.8f;
    private float _sfxVolume = 0.8f;
    private bool _hudBobbingEnabled = true;
    private bool _weaponBobbingEnabled = true;

    private const string ConfigFilePath = "user://settings.cfg";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        LoadSettings();
        GetNode<AudioManager>("/root/AudioManager")?.SetBgmVolume(_bgmVolume);
        GetNode<AudioManager>("/root/AudioManager")?.SetSfxVolume(_sfxVolume);
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp(value, 0.0f, 1.0f);
            EmitSignal(SignalName.BgmVolumeChanged, _bgmVolume);
            GetNode<AudioManager>("/root/AudioManager")?.SetBgmVolume(_bgmVolume);
            SaveSettings();
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Mathf.Clamp(value, 0.0f, 1.0f);
            EmitSignal(SignalName.SfxVolumeChanged, _sfxVolume);
            GetNode<AudioManager>("/root/AudioManager")?.SetSfxVolume(_sfxVolume);
            SaveSettings();
        }
    }

    public bool HudBobbingEnabled
    {
        get => _hudBobbingEnabled;
        set
        {
            _hudBobbingEnabled = value;
            EmitSignal(SignalName.HudBobbingToggled, _hudBobbingEnabled);
            SaveSettings();
        }
    }

    public bool WeaponBobbingEnabled
    {
        get => _weaponBobbingEnabled;
        set
        {
            _weaponBobbingEnabled = value;
            EmitSignal(SignalName.WeaponBobbingToggled, _weaponBobbingEnabled);
            SaveSettings();
        }
    }

    private void SaveSettings()
    {
        var configFile = new ConfigFile();
        configFile.SetValue("audio", "bgm_volume", _bgmVolume);
        configFile.SetValue("audio", "sfx_volume", _sfxVolume);
        configFile.SetValue("visual", "hud_bobbing", _hudBobbingEnabled);
        configFile.SetValue("visual", "weapon_bobbing", _weaponBobbingEnabled);
        Error err = configFile.Save(ConfigFilePath);
    }

    private void LoadSettings()
    {
        var configFile = new ConfigFile();
        Error err = configFile.Load(ConfigFilePath);
        if (err != Error.Ok)
        {
            if (err == Error.FileNotFound)
            {
                GD.Print("SettingsManager: using defaults creating one");
                SaveSettings();
            }
            return;
        }

        _bgmVolume = (float)configFile.GetValue("audio", "bgm_volume", _bgmVolume);
        _sfxVolume = (float)configFile.GetValue("audio", "sfx_volume", _sfxVolume);
        _hudBobbingEnabled = (bool)configFile.GetValue("visual", "hud_bobbing", _hudBobbingEnabled);
        _weaponBobbingEnabled = (bool)
            configFile.GetValue("visual", "weapon_bobbing", _weaponBobbingEnabled);

        _bgmVolume = Mathf.Clamp(_bgmVolume, 0.0f, 1.0f);
        _sfxVolume = Mathf.Clamp(_sfxVolume, 0.0f, 1.0f);

        GD.Print("SettingsManager: loaded successfully");
    }
}
