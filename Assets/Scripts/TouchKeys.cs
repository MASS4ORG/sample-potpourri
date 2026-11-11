using Guinevere;
using Turian.Engine.UI;
using GuiColor = Guinevere.Color;
using TouchPhase = Turian.Engine.Core.TouchPhase;

namespace Usercode;

/// <summary>
/// On-screen keys: while a finger is on a button its key is held, so a scene written for a keyboard plays on a touch
/// screen without changing how it reads input.
/// </summary>
[TypeId("7c1e4b90-52d3-4a86-9f10-6b3d2e8a4c17")]
public sealed class TouchKeys : Component
{
    const float PadCell = 64;
    const float ActionWidth = 112;
    const float ActionHeight = 56;
    const float Gap = 8;
    const float Margin = 14;
    const float MenuWidth = 112;
    const float MenuHeight = 56;

    // Light enough to stand out on dark scenes, dark enough for white labels on light ones.
    static readonly GuiColor Idle = GuiColor.FromArgb(225, 74, 88, 116);
    static readonly GuiColor Held = GuiColor.FromArgb(240, 74, 144, 226);

    static TouchKeys? current;
    static float scale = 1f;
    static float safeTop;
    static float reservedTop;
    static Vector2? aim;

    readonly List<Button> buttons = [];
    readonly HashSet<Key> held = [];
    readonly HashSet<int> knownFingers = [];
    readonly HashSet<int> controlFingers = [];
    UiDocumentComponent? overlay;
    Vector2 canvas;

    /// <summary>How many screen pixels one interface unit covers.</summary>
    public static float Scale => scale;

    /// <summary>The top edge free of the display cutout, in interface units.</summary>
    public static float SafeTop => safeTop;

    /// <summary>Where the latest finger on the scene itself is, in screen pixels; null before the first one.</summary>
    public static Vector2? Aim => aim;

    /// <summary>The directional keys: up, left, down, right; or left, right for a two-way pad. Empty for none.</summary>
    public string Pad { get; set; } = "";

    /// <summary>The action keys as <c>Key=Label</c> pairs separated by semicolons.</summary>
    public string Actions { get; set; } = "";

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The screen's cutouts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public SafeAreaService? SafeArea { get; private set; }

    /// <summary>
    /// Keeps the band above <paramref name="bottom"/> for the interface, in interface units. Several panels reserve
    /// in turn each frame, so the band covers the lowest; it starts empty with each screen.
    /// </summary>
    public static void ReserveTop(float bottom) => reservedTop = MathF.Max(reservedTop, bottom);

    /// <summary>The bottom of the band kept at the top while a screen with touch keys is open, in interface units.</summary>
    public static float ReservedTop => current is null ? 0 : reservedTop;

    /// <summary>Whether a screen point belongs to the controls rather than to the scene.</summary>
    /// <param name="pixel">The point in screen pixels.</param>
    public static bool Covers(Vector2 pixel) => current?.IsControl(pixel) == true;

    /// <summary>The fingers playing on the scene itself, without those that landed on a control.</summary>
    /// <param name="input">The input to read.</param>
    public static List<TouchPoint> SceneFingers(IInputSource input) =>
        [.. input.Touches.Where(static touch => touch.Phase is not (TouchPhase.Ended or TouchPhase.Canceled)
            && current?.controlFingers.Contains(touch.Id) != true && !Covers(touch.Position))];

    /// <inheritdoc />
    public override void OnAwake()
    {
        current = this;
        reservedTop = 0;
        aim = null;
        overlay = Node!.GetComponent<UiDocumentComponent>();
        overlay?.OnBuild = Build;
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        overlay?.OnBuild = null;
        ReleaseAll();
        if (ReferenceEquals(current, this))
        {
            current = null;
        }
    }

    bool IsControl(Vector2 pixel)
    {
        var point = pixel / scale;
        return point.Y <= reservedTop
            || (point.X >= canvas.X - MenuWidth - Margin * 2 && point.Y >= canvas.Y - MenuHeight - Margin * 2)
            || buttons.Exists(button => button.Contains(point));
    }

    void Build(Gui gui)
    {
        if (Input is not BufferedInputSource source)
        {
            return;
        }

        canvas = new Vector2(gui.ScreenRect.W, gui.ScreenRect.H);
        var pixels = SafeArea?.ScreenSize ?? default;
        scale = pixels.X > 0 && canvas.X > 0 ? pixels.X / canvas.X : 1f;
        var insets = SafeArea?.Insets ?? default;
        safeTop = insets.Top / scale;
        Arrange(MathF.Max(Margin, insets.Left / scale), MathF.Max(Margin, insets.Right / scale),
            MathF.Max(Margin, insets.Bottom / scale));
        foreach (var button in buttons)
        {
            using (gui.Node(button.Width, button.Height, "touch-" + button.Key).Absolute(button.X, button.Y)
                       .AlignContent(0.5f).Enter())
            {
                if (gui.Pass == Pass.Pass2Render)
                {
                    gui.DrawRect(gui.CurrentNode.Rect, held.Contains(button.Key) ? Held : Idle, 10);
                }

                gui.DrawText(button.Label, 18, GuiColor.White, centerInRect: true, clip: true);
            }
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            Track(source);
        }
    }

    void Arrange(float left, float right, float bottom)
    {
        buttons.Clear();
        var padWidth = ArrangePad(left, bottom);
        var actions = Actions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var room = canvas.X - left - right - (padWidth > 0 ? padWidth + Gap : 0);
        var columns = Math.Max(1, (int)((room + Gap) / (ActionWidth + Gap)));
        var bottomRow = canvas.Y - bottom - MenuHeight - Gap - ActionHeight;
        for (var i = 0; i < actions.Length; i++)
        {
            var parts = actions[i].Split('=', 2);
            var x = canvas.X - right - ActionWidth - (i % columns) * (ActionWidth + Gap);
            var y = bottomRow - (i / columns) * (ActionHeight + Gap);
            buttons.Add(new Button(Enum.Parse<Key>(parts[0]), parts[^1], x, y, ActionWidth, ActionHeight));
        }
    }

    float ArrangePad(float left, float bottom)
    {
        var keys = Pad.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Enum.Parse<Key>).ToArray();
        var step = PadCell + Gap;
        var y = canvas.Y - bottom - PadCell;
        switch (keys.Length)
        {
            case 4:
                buttons.Add(new Button(keys[0], "^", left + step, y - step, PadCell, PadCell));
                buttons.Add(new Button(keys[1], "<", left, y, PadCell, PadCell));
                buttons.Add(new Button(keys[2], "v", left + step, y, PadCell, PadCell));
                buttons.Add(new Button(keys[3], ">", left + step * 2, y, PadCell, PadCell));
                return PadCell * 3 + Gap * 2;
            case 2:
                buttons.Add(new Button(keys[0], "<", left, y, PadCell, PadCell));
                buttons.Add(new Button(keys[1], ">", left + step, y, PadCell, PadCell));
                return PadCell * 2 + Gap;
            default:
                return 0;
        }
    }

    // Keys change here, after this frame's tick rolled the input edges, so every script sees them in the next tick.
    void Track(BufferedInputSource source)
    {
        var fingers = source.Touches.Where(static touch =>
            touch.Phase is not (TouchPhase.Ended or TouchPhase.Canceled)).ToList();
        knownFingers.IntersectWith(fingers.Select(static touch => touch.Id));
        controlFingers.IntersectWith(fingers.Select(static touch => touch.Id));
        HashSet<Key> pressed = [];
        foreach (var finger in fingers)
        {
            if (knownFingers.Add(finger.Id) && IsControl(finger.Position))
            {
                controlFingers.Add(finger.Id);
            }

            var point = finger.Position / scale;
            foreach (var button in buttons.Where(button => button.Contains(point)))
            {
                pressed.Add(button.Key);
            }

            if (!controlFingers.Contains(finger.Id) && !IsControl(finger.Position))
            {
                aim = finger.Position;
            }
        }

        foreach (var key in held.Except(pressed).ToList())
        {
            source.PushKeyUp(key);
            held.Remove(key);
        }

        foreach (var key in pressed.Except(held).ToList())
        {
            source.PushKeyDown(key);
            held.Add(key);
        }
    }

    void ReleaseAll()
    {
        if (Input is BufferedInputSource source)
        {
            foreach (var key in held)
            {
                source.PushKeyUp(key);
            }
        }

        held.Clear();
    }

    readonly record struct Button(Key Key, string Label, float X, float Y, float Width, float Height)
    {
        public bool Contains(Vector2 point) =>
            point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
    }
}
