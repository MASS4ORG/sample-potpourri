using Microsoft.Extensions.DependencyInjection;

namespace Usercode;

/// <summary>Opens another stage scene when a designer-wired event is raised, after the frame's updates finish.</summary>
[TypeId("6ca80933-8f7d-4bba-b5eb-02b3fa63b4fb")]
public sealed class SceneLink : Component
{
    IDisposable? subscription;

    /// <summary>Event raised by a menu or back button.</summary>
    public DataAssetEvent? Trigger { get; set; }

    /// <summary>Scene loaded in place of every currently loaded scene.</summary>
    public AssetReference<Prefab> Scene { get; set; } = new();

    /// <inheritdoc/>
    public override void OnStart()
    {
        var scheduler = Services?.GetService<GameLoopScheduler>();
        var scenes = Services?.GetService<ISceneManager>();
        if (Trigger is null || Scene.IsEmpty || scheduler is null || scenes is null) { return; }
        var target = Scene.AssetId;
        // Unloading the raising scene inside its own UI or update pass is unsafe, so the switch is deferred.
        subscription = Trigger.Subscribe(() => scheduler.Post(
            () => scheduler.Forget(new ValueTask(scenes.LoadSceneAsync(target))), this), owner: Id);
    }

    /// <inheritdoc/>
    public override void OnDisable() => subscription?.Dispose();
}
