using Guinevere;
using Turian.Engine.UI;
using GuiColor = Guinevere.Color;

namespace Usercode;

/// <summary>Draws the demo scenes as tabs at the top center and loads one on a click or its number key.</summary>
/// <remarks>
/// The next scene loads additively and the current one unloads after it, so the game view never goes a frame
/// without a camera.
/// </remarks>
public sealed class SceneTabs : Component
{
    /// <summary>The demo scenes in tab order, with the prefab asset each tab loads.</summary>
    public static readonly IReadOnlyList<(string Title, Guid Scene)> Scenes =
    [
        ("Box pile", new Guid("254ee423-9017-495b-a28d-7ccfb33b92fb")),
        ("Box rain", new Guid("5d063605-4be9-4906-ab76-d149b9c3ad93")),
        ("Tower shooter", new Guid("ebe6a31a-cba9-4c44-b8e2-fca9a949f957")),
        ("Materials", new Guid("4b5b168f-da43-4a5d-aee1-0dbbdb0594ae")),
        ("Physics lab", new Guid("59d23631-8064-46bd-b15d-5cc57d6f0c92")),
        ("Controller", new Guid("41b00a1d-37a3-463b-b8f1-886b0cae7a01")),
        ("Joints", new Guid("41b00a1d-37a3-463b-b8f1-886b0cae7a02")),
        ("Vehicle", new Guid("41b00a1d-37a3-463b-b8f1-886b0cae7a03")),
        ("Ragdoll", new Guid("41b00a1d-37a3-463b-b8f1-886b0cae7a04")),
    ];

    static readonly Key[] NumberKeys =
        [Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5, Key.Number6, Key.Number7, Key.Number8, Key.Number9];
    static readonly Key[] FunctionKeys = [Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9];
    static readonly GuiColor Selected = GuiColor.FromArgb(240, 74, 144, 226);
    static readonly GuiColor Hovered = GuiColor.FromArgb(235, 42, 52, 70);

    /// <summary>The height of the scene selector.</summary>
    public const float BarHeight = 52;

    const float StepWidth = 72;

    UiDocumentComponent? overlay;
    int requested = -1;
    bool loading;

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The session's scene manager, absent in hand-built test hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public ISceneManager? SceneManager { get; private set; }

    /// <summary>The index of the scene this tab bar belongs to, or -1 when it is not a demo scene.</summary>
    [JsonIgnore, Hide]
    public int Current => Scene() is { } scene ? IndexOf(scene.Title) : -1;

    /// <summary>Where the tab bar was drawn last frame, in game view pixels.</summary>
    [JsonIgnore, Hide]
    public Rect Bounds { get; private set; }

    /// <summary>Whether a game view point is over the tab bar, so scenes can ignore clicks meant for it.</summary>
    public bool Contains(Vector2 point) => TouchKeys.Covers(point);

    /// <inheritdoc />
    public override void OnAwake()
    {
        overlay = Node!.GetComponent<UiDocumentComponent>();
        overlay?.OnBuild = Build;
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        for (var i = 0; i < Scenes.Count && Input is not null; i++)
        {
            if (Input.WasKeyPressed(NumberKeys[i]) || Input.WasKeyPressed(FunctionKeys[i]))
            {
                requested = i;
            }
        }

        if (requested < 0 || loading || SceneManager is null)
        {
            return;
        }

        var index = requested;
        requested = -1;
        if (index != Current)
        {
            loading = true;
            _ = Load(SceneManager, index);
        }
    }

    /// <inheritdoc />
    public override void OnDestroy() => overlay?.OnBuild = null;

    async Task Load(ISceneManager scenes, int index)
    {
        var previous = scenes.ActiveScene;
        try
        {
            var loaded = await scenes.LoadSceneAsync(Scenes[index].Scene, LoadSceneMode.Additive);
            if (previous is not null && !ReferenceEquals(previous, loaded))
            {
                scenes.UnloadScene(previous);
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or KeyNotFoundException)
        {
            loading = false;
            Log.Logger.LogError(error, "Could not load the {Title} demo", Scenes[index].Title);
        }
    }

    void Build(Gui gui)
    {
        var current = Math.Max(0, Current);
        var width = MathF.Min(gui.ScreenRect.W - 24, 440);
        var x = (gui.ScreenRect.W - width) * 0.5f;
        var top = TouchKeys.SafeTop + 12;
        Bounds = new Rect(x, top, width, BarHeight);
        TouchKeys.ReserveTop(top + BarHeight);
        using (gui.Node(width, BarHeight, "scene-selector").Absolute(x, top).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawRect(gui.CurrentNode.Rect, DemoHud.Panel, 10);
            }

            Step(gui, "<", (current + Scenes.Count - 1) % Scenes.Count);
            using (gui.Node(width - StepWidth * 2, BarHeight, "scene-title").AlignContent(0.5f).Enter())
            {
                gui.DrawText(string.Create(CultureInfo.InvariantCulture, $"{current + 1}/{Scenes.Count}  {Scenes[current].Title}"), 20,
                    GuiColor.White, centerInRect: true, clip: true);
            }

            Step(gui, ">", (current + 1) % Scenes.Count);
        }
    }

    void Step(Gui gui, string label, int target)
    {
        using (gui.Node(StepWidth, BarHeight, "scene-step-" + label).AlignContent(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawRect(gui.CurrentNode.Rect, Hovered, 10);
                if (interactable.OnClick())
                {
                    requested = target;
                }
            }

            gui.DrawText(label, 26, GuiColor.White, centerInRect: true, clip: true);
        }
    }

    IDemoScene? Scene() => Node?.Parent?.Components.OfType<IDemoScene>().FirstOrDefault();

    static int IndexOf(string title)
    {
        for (var i = 0; i < Scenes.Count; i++)
        {
            if (Scenes[i].Title == title)
            {
                return i;
            }
        }

        return -1;
    }
}
