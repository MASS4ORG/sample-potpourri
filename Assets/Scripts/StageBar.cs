using Guinevere;
using Turian.Engine.UI;
using GuiColor = Guinevere.Color;

namespace Usercode;

/// <summary>
/// The controls every screen shares: a frame-rate readout in the top-right corner and a Back button in the
/// bottom-right one, which raises the same event as Escape and the phone's Back button.
/// </summary>
[TypeId("3f9a7d12-8e64-4c05-b2c1-5a0e7f316d48")]
public sealed class StageBar : UiDocumentComponent
{
    const float ButtonWidth = 112;
    const float ButtonHeight = 56;
    const float Margin = 14;
    const float RateWidth = 112;

    static readonly GuiColor Panel = GuiColor.FromArgb(190, 18, 21, 29);
    static readonly GuiColor Idle = GuiColor.FromArgb(225, 74, 88, 116);
    static readonly GuiColor Pressed = GuiColor.FromArgb(240, 74, 144, 226);

    long lastFrame;
    double frameSeconds;

    /// <summary>Draws above the screen's own interface, at the size the other screens are laid out for.</summary>
    public StageBar()
    {
        SortOrder = 1000;
        ScaleMode = UiScaleMode.ScaleWithScreenSize;
        ReferenceResolution = new Int2(780, 440);
    }

    /// <summary>The event the Back button, Escape and the phone's Back button raise.</summary>
    public DataAssetEvent? Trigger { get; set; }

    /// <summary>Whether to draw the Back button; screens whose own layout has one turn it off.</summary>
    public bool ShowBack { get; set; } = true;

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The screen's cutouts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public SafeAreaService? SafeArea { get; private set; }

    /// <summary>Frames per second, smoothed over about half a second.</summary>
    [JsonIgnore, Hide]
    public double FramesPerSecond => frameSeconds > 0 ? 1 / frameSeconds : 0;

    /// <inheritdoc/>
    public override void OnAwake() => OnBuild = Build;

    /// <inheritdoc/>
    public override void OnDestroy() => OnBuild = null;

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        var now = Stopwatch.GetTimestamp();
        if (lastFrame != 0)
        {
            var seconds = Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds;
            frameSeconds = frameSeconds == 0 ? seconds : frameSeconds + (seconds - frameSeconds) * 0.05;
        }

        lastFrame = now;
        if (Input?.WasKeyPressed(Key.Escape) == true)
        {
            Trigger?.Raise();
        }
    }

    void Build(Gui gui)
    {
        var canvas = new Vector2(gui.ScreenRect.W, gui.ScreenRect.H);
        var pixels = SafeArea?.ScreenSize ?? default;
        var scale = pixels.X > 0 && canvas.X > 0 ? pixels.X / canvas.X : 1f;
        var insets = SafeArea?.Insets ?? default;
        // Below the cutout, and below a band another control keeps at the top, such as the physics demo selector.
        var top = MathF.Max(MathF.Max(Margin, insets.Top / scale), TouchKeys.ReservedTop + 8);
        var right = MathF.Max(Margin, insets.Right / scale);
        var bottom = MathF.Max(Margin, insets.Bottom / scale);

        var rate = string.Create(CultureInfo.InvariantCulture,
            $"{FramesPerSecond:0} fps {frameSeconds * 1000:0.0} ms");
        // Narrow enough to sit beside a screen title on a phone held upright.
        using (gui.Node(RateWidth, 26, "stage-rate").Absolute(canvas.X - right - RateWidth, top).AlignContent(0.5f)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawRect(gui.CurrentNode.Rect, Panel, 8);
            }

            gui.DrawText(rate, 14, GuiColor.White, centerInRect: true, clip: true);
        }

        if (!ShowBack || Trigger is null)
        {
            return;
        }

        using (gui.Node(ButtonWidth, ButtonHeight, "stage-back")
                   .Absolute(canvas.X - right - ButtonWidth, canvas.Y - bottom - ButtonHeight).AlignContent(0.5f)
                   .Enter())
        {
            var interactable = gui.GetInteractable();
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawRect(gui.CurrentNode.Rect, interactable.OnHold() ? Pressed : Idle, 10);
                if (interactable.OnClick())
                {
                    Trigger.Raise();
                }
            }

            gui.DrawText("Back", 18, GuiColor.White, centerInRect: true, clip: true);
        }
    }
}
