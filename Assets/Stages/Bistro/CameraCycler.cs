namespace Usercode;

/// <summary>
/// Cycles a fixed list of cameras through render focus, one at a time, on a fixed interval — a
/// "teaser" rig for stringing several framed viewpoints into one clip. Only the camera whose
/// <see cref="CameraComponent.Priority"/> is currently highest actually renders
/// (<see cref="CameraComponent.FindPrimary"/>), so cycling is just handing that highest value
/// around the wired-up list.
/// </summary>
public class CameraCycler : Component
{
    /// <summary>
    /// The cameras to cycle through, in order. Wire these up in the Inspector — drag each camera
    /// node from the Scene Tree onto a slot — rather than discovering cameras automatically, so a
    /// debug or utility camera added to the scene later doesn't silently join the rotation.
    /// </summary>
    public List<CameraComponent?> Cameras { get; set; } = [];

    /// <summary>Seconds each camera holds focus before the next one takes over.</summary>
    public float SecondsPerCamera { get; set; } = 5f;

    /// <summary>Priority given to the currently featured camera. Every other camera gets 0.</summary>
    public int FeaturedPriority { get; set; } = 100;

    private List<CameraComponent> resolved = [];
    private int index = -1;
    private float elapsed;

    /// <inheritdoc/>
    public override void OnStart()
    {
        resolved = [.. Cameras.OfType<CameraComponent>()];
        if (resolved.Count == 0)
        {
            Log.Logger.LogWarning("CameraCycler: no camera references resolved; nothing to cycle. Wire up {Cameras} in the Inspector.", nameof(Cameras));
            return;
        }

        Advance();
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (resolved.Count < 2) return;

        elapsed += deltaTime;
        if (elapsed < SecondsPerCamera) return;

        elapsed = 0f;
        Advance();
    }

    private void Advance()
    {
        index = (index + 1) % resolved.Count;
        for (var i = 0; i < resolved.Count; i++)
        {
            resolved[i].Priority = i == index ? FeaturedPriority : 0;
        }

        Log.Logger.LogInformation(
            "CameraCycler: now viewing '{CameraName}' ({Position}/{Count})",
            resolved[index].Node?.Name ?? "Detached camera",
            index + 1,
            resolved.Count);
    }
}
