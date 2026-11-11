namespace Usercode;

/// <summary>Quits the application when a designer-wired event is raised.</summary>
[TypeId("6f2b8a64-3c1e-4f0a-9a57-0d4a7b1e6c21")]
public sealed class QuitLink : Component
{
    IDisposable? subscription;

    /// <summary>Event raised by the exit button.</summary>
    public DataAssetEvent? Trigger { get; set; }

    /// <inheritdoc/>
    public override void OnStart() => subscription = Trigger?.Subscribe(static () => Application.Quit(), owner: Id);

    /// <inheritdoc/>
    public override void OnDisable() => subscription?.Dispose();
}
