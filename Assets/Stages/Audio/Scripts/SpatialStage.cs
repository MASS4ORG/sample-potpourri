using Microsoft.Extensions.DependencyInjection;
using Turian.Audio;

namespace Usercode;

/// <summary>
/// Moves the emitter around the listener (orbit) or straight away from it (sweep) and posts a tick at the emitter on
/// every interval, so panning and distance attenuation can be heard on transients as well as on a sustained tone.
/// </summary>
[TypeId("a161555d-7660-474f-953e-13510ff7c8f5")]
public sealed class SpatialStage : Component
{
    readonly List<IDisposable> subscriptions = [];
    IAudioService? audio;
    Node? emitter;
    WorldTextComponent? status;
    string tick = string.Empty;
    bool sweeping;
    float elapsed;
    float nextTick;

    /// <summary>Short spatial event posted at the emitter on every tick interval.</summary>
    public DataAssetReference<AudioEvent>? TickEvent { get; set; }
    /// <summary>Raised by the Orbit button.</summary>
    public DataAssetEvent? Orbit { get; set; }
    /// <summary>Raised by the Sweep button.</summary>
    public DataAssetEvent? Sweep { get; set; }
    /// <summary>Orbit radius in world units.</summary>
    public float Radius { get; set; } = 6;
    /// <summary>Seconds per orbit or per sweep out and back.</summary>
    public float Period { get; set; } = 12;
    /// <summary>Nearest sweep distance.</summary>
    public float NearDistance { get; set; } = 0.5f;
    /// <summary>Farthest sweep distance; beyond the event's MaxDistance the emitter is silent.</summary>
    public float FarDistance { get; set; } = 24;
    /// <summary>Seconds between ticks.</summary>
    public float TickInterval { get; set; } = 0.5f;
    /// <summary>Attenuation start used for the status readout; match the hum event's MinDistance.</summary>
    public float MinDistance { get; set; } = 1;
    /// <summary>Attenuation end used for the status readout; match the hum event's MaxDistance.</summary>
    public float MaxDistance { get; set; } = 20;

    /// <inheritdoc/>
    public override void OnStart()
    {
        audio = Services?.GetService<IAudioService>();
        if (audio is not null)
        {
            tick = audio.Prepare(TickEvent ?? throw new InvalidOperationException("Assign the tick event."));
        }
        emitter = Node?.Children.FirstOrDefault(static child => child.Name == "Emitter");
        status = Node?.Children.FirstOrDefault(static child => child.Name == "Status")?.Components
            .OfType<WorldTextComponent>().FirstOrDefault();
        if (Orbit is not null) { subscriptions.Add(Orbit.Subscribe(() => Select(false), owner: Id)); }
        if (Sweep is not null) { subscriptions.Add(Sweep.Subscribe(() => Select(true), owner: Id)); }
    }

    void Select(bool sweep)
    {
        sweeping = sweep;
        elapsed = 0;
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (emitter is null) { return; }
        elapsed += deltaTime;
        var phase = elapsed / Period % 1;
        if (sweeping)
        {
            var travel = phase < 0.5f ? phase * 2 : 2 - phase * 2;
            emitter.Position = new Vector3(0, 0, NearDistance + (FarDistance - NearDistance) * travel);
        }
        else
        {
            var angle = phase * MathF.Tau;
            emitter.Position = new Vector3(MathF.Sin(angle) * Radius, 0, MathF.Cos(angle) * Radius);
        }
        if (elapsed >= nextTick || nextTick - elapsed > TickInterval)
        {
            nextTick = elapsed + TickInterval;
            audio?.Post(tick, Id, new AudioParameterSet { Position = emitter.GlobalTransform.Position });
        }
        if (status is not null) { status.Text = Describe(emitter.Position); }
    }

    string Describe(Vector3 position)
    {
        var distance = position.Length();
        var azimuth = MathF.Atan2(-position.X, position.Z) * 180 / MathF.PI;
        var gain = 1 - Math.Clamp((distance - MinDistance) / (MaxDistance - MinDistance), 0, 1);
        return $"{(sweeping ? "Sweep" : "Orbit")}: azimuth {azimuth,4:0} deg   distance {distance,5:0.0}\n"
            + $"linear attenuation {gain:0.00}   (0 deg = ahead, +90 = right)";
    }

    /// <inheritdoc/>
    public override void OnDisable()
    {
        foreach (var subscription in subscriptions) { subscription.Dispose(); }
        subscriptions.Clear();
        audio?.StopAll(Id);
    }
}
