namespace Usercode;

/// <summary>A demo scene that the shared HUD describes and that R resets.</summary>
public interface IDemoScene
{
    /// <summary>The scene name shown at the top of the HUD.</summary>
    string Title { get; }

    /// <summary>The scene's controls and notes, one HUD line each.</summary>
    IReadOnlyList<string> Help { get; }

    /// <summary>A live, scene-specific status line.</summary>
    string Status { get; }

    /// <summary>Despawns every runtime body and builds the scene again.</summary>
    void ResetScene();
}
