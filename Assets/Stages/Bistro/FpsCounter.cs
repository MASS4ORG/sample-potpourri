namespace Usercode;

/// <summary>
/// Logs frames-per-second to the console once a second — a way to watch performance while the
/// engine has no on-screen HUD (issue #138).
/// </summary>
public class FpsCounter : Component
{
    private float elapsed;
    private int frames;

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        elapsed += deltaTime;
        frames++;

        if (elapsed < 1f) return;

        Log.Logger.LogInformation("FPS: {Fps:F1} ({FrameTimeMs:F2} ms/frame)", frames / elapsed, 1000f * elapsed / frames);
        elapsed = 0f;
        frames = 0;
    }
}
