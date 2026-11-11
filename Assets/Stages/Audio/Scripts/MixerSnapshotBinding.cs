using Microsoft.Extensions.DependencyInjection;
using Turian.Audio;

namespace Usercode;

/// <summary>Auditions an authored snapshot when a UI or gameplay event is raised.</summary>
[TypeId("336ec03c-c3fd-52f1-a3eb-f860515f92b6")]
public sealed class MixerSnapshotBinding : Component
{
    IDisposable? subscription;
    /// <summary>Event that requests the transition.</summary>
    public DataAssetEvent? Trigger { get; set; }
    /// <summary>Snapshot name in the selected mixer asset.</summary>
    public string Snapshot { get; set; } = "Normal";
    /// <summary>Crossfade duration in seconds.</summary>
    public float Seconds { get; set; } = 0.7f;

    /// <inheritdoc/>
    public override void OnStart()
    {
        var mixer = Services?.GetService<IAudioMixerService>();
        subscription = Trigger?.Subscribe(() => mixer?.TransitionTo(Snapshot, Seconds), owner: Id);
    }

    /// <inheritdoc/>
    public override void OnDisable() => subscription?.Dispose();
}
