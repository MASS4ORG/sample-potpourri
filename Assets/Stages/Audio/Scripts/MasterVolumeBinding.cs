using Microsoft.Extensions.DependencyInjection;
using Turian.Audio;

namespace Usercode;

/// <summary>Connects settings-screen variables to a named bus and reflects snapshot and Studio edits.</summary>
[TypeId("a7d82d52-9067-4d3c-83ef-a0e5ed08615b")]
public sealed class MasterVolumeBinding : Component
{
    IAudioMixerService? mixer;
    IDisposable? volumeChanges;
    IDisposable? muteChanges;
    bool synchronizing;

    /// <summary>Mixer bus controlled by these settings.</summary>
    public string Bus { get; set; } = "Master";
    /// <summary>Linear bus gain from zero through one.</summary>
    public DataAssetVariableFloat? Volume { get; set; }
    /// <summary>Whether this bus and its descendants are silent.</summary>
    public DataAssetVariableBool? Muted { get; set; }

    /// <inheritdoc/>
    public override void OnStart()
    {
        mixer = Services?.GetService<IAudioMixerService>();
        volumeChanges = Volume?.Subscribe(_ => Apply(), owner: Id);
        muteChanges = Muted?.Subscribe(_ => Apply(), owner: Id);
        Apply();
    }

    void Apply()
    {
        if (synchronizing || mixer is null) { return; }
        if (Volume is not null) { mixer.SetBusVolume(Bus, Math.Clamp(Volume.Value, 0, 1)); }
        if (Muted is not null) { mixer.SetBusMute(Bus, Muted.Value); }
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (mixer is null) { return; }
        var state = mixer.GetBus(Bus);
        synchronizing = true;
        try
        {
            if (Volume is not null) { Volume.Value = state.Volume; }
            if (Muted is not null) { Muted.Value = state.Mute; }
        }
        finally { synchronizing = false; }
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        volumeChanges?.Dispose();
        muteChanges?.Dispose();
    }
}
