// AudioManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class AudioManager : Node
{
    [Export]
    private int SfxPoolSize = 10;

    private AudioStreamPlayer _bgmPlayer;
    private Node _sfxPlayerContainer;
    private List<AudioStreamPlayer> _sfxPlayers = new List<AudioStreamPlayer>();
    private Dictionary<string, AudioStream> _soundCache = new Dictionary<string, AudioStream>();

    private const string MasterBusName = "Master";
    private const string BgmBusName = "BGM";
    private const string SfxBusName = "SFX";

    private int _masterBusIndex;
    private int _bgmBusIndex;
    private int _sfxBusIndex;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _bgmPlayer = new AudioStreamPlayer();
        _bgmPlayer.Name = "BGMPlayer";
        _bgmPlayer.Bus = BgmBusName;
        AddChild(_bgmPlayer);
        GD.Print("AudioManager: BGM Player created.");

        _sfxPlayerContainer = new Node();
        _sfxPlayerContainer.Name = "SFXPlayers";
        AddChild(_sfxPlayerContainer);

        for (int i = 0; i < SfxPoolSize; i++)
        {
            var sfxPlayer = new AudioStreamPlayer();
            sfxPlayer.Name = $"SFXPlayer_{i}";
            sfxPlayer.Bus = SfxBusName;
            _sfxPlayers.Add(sfxPlayer);
            _sfxPlayerContainer.AddChild(sfxPlayer);
        }
        GD.Print($"AudioManager: Created SFX Player Pool with {SfxPoolSize} players.");

        _masterBusIndex = AudioServer.GetBusIndex(MasterBusName);
        _bgmBusIndex = AudioServer.GetBusIndex(BgmBusName);
        _sfxBusIndex = AudioServer.GetBusIndex(SfxBusName);

        if (_masterBusIndex == -1)
            GD.PrintErr($"AudioManager Error: Master bus '{MasterBusName}' not found");
        if (_bgmBusIndex == -1)
            GD.PrintErr($"AudioManager Error: BGM bus '{BgmBusName}' not found");
        if (_sfxBusIndex == -1)
            GD.PrintErr($"AudioManager Error: SFX bus '{SfxBusName}' not found");

        GD.Print("AudioManager Ready.");
    }

    private AudioStream LoadSound(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            GD.PrintErr("AudioManager Error: LoadSound called with null or empty path.");
            return null;
        }

        if (_soundCache.TryGetValue(path, out AudioStream cachedSound))
        {
            return cachedSound;
        }

        var sound = ResourceLoader.Load<AudioStream>(path);
        if (sound == null)
        {
            GD.PrintErr($"AudioManager Error: Failed to load sound at path: {path}");
            return null;
        }

        _soundCache.Add(path, sound);
        GD.Print($"AudioManager: Loaded and cached sound '{path}'");
        return sound;
    }

    public void PlayBGM(string path, float volumeDb = 0f, bool loop = true)
    {
        AudioStream stream = LoadSound(path);
        if (stream == null)
            return;

        _bgmPlayer.Stream = stream;
        _bgmPlayer.VolumeDb = volumeDb;
        _bgmPlayer.Play();
        GD.Print($"AudioManager: Playing BGM '{path}'");
    }

    public void StopBGM()
    {
        _bgmPlayer.Stop();
        GD.Print("AudioManager: Stopping BGM");
    }

    public void SetBGMPaused(bool paused)
    {
        _bgmPlayer.StreamPaused = paused;
        GD.Print($"AudioManager: BGM Paused = {paused}");
    }

    public void PlaySFX(string path, float volumeDb = 0f)
    {
        AudioStream stream = LoadSound(path);
        if (stream == null)
            return;

        AudioStreamPlayer sfxPlayer = _sfxPlayers.FirstOrDefault(p => !p.Playing);

        if (sfxPlayer == null)
        {
            GD.Print("AudioManager Warning: No available SFX players. Sound not played.");
            return;
        }

        sfxPlayer.Stream = stream;
        sfxPlayer.VolumeDb = volumeDb;
        sfxPlayer.Play();
    }

    public void SetMasterVolume(float linearVolume)
    {
        if (_masterBusIndex != -1)
        {
            AudioServer.SetBusVolumeDb(
                _masterBusIndex,
                Mathf.LinearToDb(Mathf.Clamp(linearVolume, 0.0f, 1.0f))
            );
        }
    }

    public void SetBgmVolume(float linearVolume)
    {
        if (_bgmBusIndex != -1)
        {
            AudioServer.SetBusVolumeDb(
                _bgmBusIndex,
                Mathf.LinearToDb(Mathf.Clamp(linearVolume, 0.0f, 1.0f))
            );
        }
    }

    public void SetSfxVolume(float linearVolume)
    {
        if (_sfxBusIndex != -1)
        {
            AudioServer.SetBusVolumeDb(
                _sfxBusIndex,
                Mathf.LinearToDb(Mathf.Clamp(linearVolume, 0.0f, 1.0f))
            );
        }
    }

    public float GetMasterVolume()
    {
        return _masterBusIndex != -1
            ? Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_masterBusIndex))
            : 0f;
    }

    public float GetBgmVolume()
    {
        return _bgmBusIndex != -1 ? Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_bgmBusIndex)) : 0f;
    }

    public float GetSfxVolume()
    {
        return _sfxBusIndex != -1 ? Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_sfxBusIndex)) : 0f;
    }
}
