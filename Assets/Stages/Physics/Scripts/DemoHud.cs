using Guinevere;
using Turian.Engine.UI;
using GuiColor = Guinevere.Color;

namespace Usercode;

/// <summary>Draws the scene's live status under the scene selector.</summary>
public sealed class DemoHud : Component
{
    /// <summary>The background of every demo panel.</summary>
    public static readonly GuiColor Panel = GuiColor.FromArgb(225, 16, 23, 35);

    /// <summary>Titles and highlighted values.</summary>
    public static readonly GuiColor Heading = GuiColor.FromArgb(255, 222, 136);

    /// <summary>Live measurements.</summary>
    public static readonly GuiColor Stats = GuiColor.FromArgb(110, 218, 248);

    /// <summary>Secondary text.</summary>
    public static readonly GuiColor Muted = GuiColor.FromArgb(170, 180, 196);

    const float LineHeight = 24;

    UiDocumentComponent overlay = null!;
    IDemoScene? scene;

    /// <summary>The top of the status panels, below the scene selector.</summary>
    public static float PanelTop => TouchKeys.SafeTop + 12 + SceneTabs.BarHeight + 6;

    /// <summary>The game view size in screen pixels, known after the overlay's first layout pass.</summary>
    [JsonIgnore, Hide]
    public Vector2 ViewportSize { get; private set; }

    /// <summary>The width of a status panel on this screen.</summary>
    public static float PanelWidth(Gui gui) => MathF.Min(gui.ScreenRect.W - 24, 440);

    /// <summary>Draws one line of panel text.</summary>
    public static void Label(Gui gui, string text, GuiColor color, float size)
    {
        using (gui.Node(-1, LineHeight).ExpandWidth().Enter())
        {
            gui.DrawText(text, size, color, centerInRect: false, clip: true);
        }
    }

    /// <inheritdoc />
    public override void OnAwake()
    {
        overlay = Node!.GetComponent<UiDocumentComponent>()!;
        overlay.OnBuild = Build;
        scene = Node.Components.OfType<IDemoScene>().FirstOrDefault();
    }

    /// <inheritdoc />
    public override void OnDestroy() => overlay.OnBuild = null;

    void Build(Gui gui)
    {
        ViewportSize = new Vector2(gui.ScreenRect.W, gui.ScreenRect.H) * TouchKeys.Scale;
        var width = PanelWidth(gui);
        using (gui.Node(width, LineHeight + 12, "demo-hud").Absolute((gui.ScreenRect.W - width) * 0.5f, PanelTop)
                   .Padding(6).Direction(Axis.Vertical).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawRect(gui.CurrentNode.Rect, Panel, 10);
            }

            Label(gui, scene?.Status ?? "", Heading, 15);
        }
    }
}
