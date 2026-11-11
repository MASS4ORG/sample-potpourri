using Microsoft.Extensions.DependencyInjection;
using Turian.Audio;

namespace Usercode;

/// <summary>Posts a prepared audio event whenever a designer-wired gameplay event is raised.</summary>
[TypeId("6e28e560-8437-458f-a7d1-02f29489bfd4")]
public sealed class EventSound : Component
{
    IAudioService? audio;
    IDisposable? subscription;
    string sound = string.Empty;

    /// <summary>Audio event prepared at scene startup.</summary>
    public DataAssetReference<AudioEvent>? Sound { get; set; }

    /// <summary>Gameplay event that plays the sound.</summary>
    public DataAssetEvent? Trigger { get; set; }

    /// <inheritdoc/>
    public override void OnStart()
    {
        audio = Services?.GetService<IAudioService>();
        if (audio is not null)
        {
            sound = audio.Prepare(Sound ?? throw new InvalidOperationException("Assign the sound before starting the scene."));
        }
        subscription = Trigger?.Subscribe(() => audio?.Post(sound, Id, new AudioParameterSet()), owner: Id);
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        subscription?.Dispose();
        audio?.StopAll(Id);
    }
}
