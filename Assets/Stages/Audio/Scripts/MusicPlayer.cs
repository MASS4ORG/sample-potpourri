using Microsoft.Extensions.DependencyInjection;
using Turian.Audio;

namespace Usercode;

/// <summary>Streams the MP3 and FLAC tracks with play, pause, stop and loop controls raised by the stage UI.</summary>
[TypeId("4ebc6d4d-bcbf-4d5c-860d-1792d7e6b4c7")]
public sealed class MusicPlayer : Component
{
    readonly List<IDisposable> subscriptions = [];
    IAudioService? audio;
    WorldTextComponent? status;
    string mp3Loop = string.Empty;
    string mp3Once = string.Empty;
    string flacLoop = string.Empty;
    string flacOnce = string.Empty;
    string track = "Stopped";
    bool paused;
    float refresh;

    /// <summary>Looping streamed MP3 event.</summary>
    public DataAssetReference<AudioEvent>? Mp3Loop { get; set; }
    /// <summary>Single-pass streamed MP3 event.</summary>
    public DataAssetReference<AudioEvent>? Mp3Once { get; set; }
    /// <summary>Looping streamed FLAC event.</summary>
    public DataAssetReference<AudioEvent>? FlacLoop { get; set; }
    /// <summary>Single-pass streamed FLAC event.</summary>
    public DataAssetReference<AudioEvent>? FlacOnce { get; set; }
    /// <summary>Raised by the Play MP3 button.</summary>
    public DataAssetEvent? PlayMp3 { get; set; }
    /// <summary>Raised by the Play FLAC button.</summary>
    public DataAssetEvent? PlayFlac { get; set; }
    /// <summary>Raised by the Pause / Resume button.</summary>
    public DataAssetEvent? PauseToggle { get; set; }
    /// <summary>Raised by the Stop button.</summary>
    public DataAssetEvent? Stop { get; set; }
    /// <summary>Whether the next Play selects the looping event.</summary>
    public DataAssetVariableBool? Loop { get; set; }

    /// <inheritdoc/>
    public override void OnStart()
    {
        audio = Services?.GetService<IAudioService>();
        if (audio is not null)
        {
            mp3Loop = audio.Prepare(Mp3Loop ?? throw new InvalidOperationException("Assign Mp3Loop."));
            mp3Once = audio.Prepare(Mp3Once ?? throw new InvalidOperationException("Assign Mp3Once."));
            flacLoop = audio.Prepare(FlacLoop ?? throw new InvalidOperationException("Assign FlacLoop."));
            flacOnce = audio.Prepare(FlacOnce ?? throw new InvalidOperationException("Assign FlacOnce."));
        }
        status = Node?.Children.SelectMany(static child => child.Components).OfType<WorldTextComponent>().FirstOrDefault();
        Subscribe(PlayMp3, () => Begin("MP3 5:00 44.1 kHz", Loop?.Value != false
            ? audio?.Post(mp3Loop, Id, new AudioParameterSet()) : audio?.Post(mp3Once, Id, new AudioParameterSet())));
        Subscribe(PlayFlac, () => Begin("FLAC 3:30 48 kHz", Loop?.Value != false
            ? audio?.Post(flacLoop, Id, new AudioParameterSet()) : audio?.Post(flacOnce, Id, new AudioParameterSet())));
        Subscribe(PauseToggle, TogglePause);
        Subscribe(Stop, () =>
        {
            audio?.StopAll(Id, 0.1f);
            track = "Stopped";
            paused = false;
        });
    }

    void Subscribe(DataAssetEvent? trigger, Action action)
    {
        if (trigger is not null) { subscriptions.Add(trigger.Subscribe(action, owner: Id)); }
    }

    // Stopping first keeps one track audible; the fade avoids a click when switching tracks.
    void Begin(string name, VoiceHandle? voice)
    {
        track = voice is { Value: not 0 } ? $"{name}{(Loop?.Value != false ? ", looping" : string.Empty)}"
            : "Dropped: no free stream";
        paused = false;
        refresh = 0.3f;
    }

    void TogglePause()
    {
        if (track == "Stopped") { return; }
        paused = !paused;
        audio?.Pause(Id, paused);
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        refresh -= deltaTime;
        if (status is null || audio is null || refresh > 0) { return; }
        refresh = 0.25f;
        var stats = audio.Statistics;
        if (track != "Stopped" && stats.ActiveVoices == 0) { track = "Finished"; }
        status.Text = $"{track}{(paused ? " (paused)" : string.Empty)}\n"
            + $"streams {stats.ActiveStreams}   voices {stats.ActiveVoices}   underruns {stats.StreamUnderruns}";
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        foreach (var subscription in subscriptions) { subscription.Dispose(); }
        subscriptions.Clear();
        audio?.StopAll(Id);
    }
}
