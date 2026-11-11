using Guinevere;
using Turian.Engine.UI;

namespace Usercode;

/// <summary>Draws frame rate and physics statistics in the top-right corner; reused by every demo scene.</summary>
public sealed class PhysicsStatsPanel : Component
{
    const float Width = 290;
    const float LineHeight = 20;

    UiDocumentComponent? overlay;
    long lastFrame;
    double frameSeconds;

    /// <summary>The physics service, absent when the physics bricks are not installed.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public PhysicsScene? Physics { get; private set; }

    /// <summary>Frames per second, smoothed over about half a second of real time.</summary>
    [JsonIgnore, Hide]
    public double FramesPerSecond => frameSeconds > 0 ? 1 / frameSeconds : 0;

    /// <summary>The physics statistics, or zeros without a physics backend.</summary>
    public static PhysicsStatistics ReadStats(PhysicsScene? physics) => physics?.Statistics ?? default;

    /// <summary>
    /// One line of physics statistics, shared by the panel and the benchmark logs. Tracked pairs are those the last
    /// step found touching; resting pairs of sleeping bodies without collision handlers are not tracked, so a settled
    /// pile shows none. Logs before this label said pairs= and counted those resting pairs too.
    /// </summary>
    public static string FormatStats(PhysicsStatistics stats) => string.Create(CultureInfo.InvariantCulture,
        $"bodies={stats.Bodies - stats.StaticBodies} awake={stats.AwakeBodies} sleeping={stats.SleepingBodies} " +
        $"tracked={stats.ContactPairs} step={stats.StepMilliseconds:0.00}ms");

    /// <inheritdoc />
    public override void OnAwake()
    {
        overlay = Node!.GetComponent<UiDocumentComponent>();
        overlay?.OnBuild = Build;
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        var now = Stopwatch.GetTimestamp();
        if (lastFrame != 0)
        {
            var seconds = Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds;
            frameSeconds = frameSeconds == 0 ? seconds : frameSeconds + (seconds - frameSeconds) * 0.05;
        }

        lastFrame = now;
    }

    /// <inheritdoc />
    public override void OnDestroy() => overlay?.OnBuild = null;

    void Build(Gui gui)
    {
        var stats = ReadStats(Physics);
        // The frame rate is the stage bar's; this line keeps to physics.
        var line = Physics?.IsSimulating == true
            ? Format($"bodies {stats.Bodies - stats.StaticBodies}  awake {stats.AwakeBodies}  step {stats.StepMilliseconds:0.00} ms")
            : Format($"physics: none");
        var width = DemoHud.PanelWidth(gui);
        TouchKeys.ReserveTop(DemoHud.PanelTop + 40 + 36);
        using (gui.Node(width, 36, "physics-stats").Absolute((gui.ScreenRect.W - width) * 0.5f, DemoHud.PanelTop + 40)
                   .Padding(6).Direction(Axis.Vertical).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawRect(gui.CurrentNode.Rect, DemoHud.Panel, 10);
            }

            DemoHud.Label(gui, line, DemoHud.Stats, 15);
        }
    }

    static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
