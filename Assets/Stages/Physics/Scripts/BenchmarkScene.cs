namespace Usercode;

/// <summary>
/// The shared part of the box benchmarks: the scene starts empty, R starts or restarts a run, P pauses, and the
/// statistics are logged every <see cref="ReportInterval"/> and once when every box sleeps.
/// </summary>
public abstract class BenchmarkScene : Component, IDemoScene
{
    static readonly Color[] Palette =
    [
        Color.FromHex("#e8604c"), Color.FromHex("#f2b134"), Color.FromHex("#4fb477"),
        Color.FromHex("#3e8ed0"), Color.FromHex("#9b6dd6"), Color.FromHex("#e6e1d3"),
    ];

    double sinceReport;
    double sinceSweep;

    /// <summary>The edge length of each box in meters.</summary>
    public float BoxSize { get; set; } = 1f;

    /// <summary>The random seed, so every run starts from the same boxes.</summary>
    public int Seed { get; set; } = 1234;

    /// <summary>Seconds of simulation between benchmark log lines.</summary>
    public float ReportInterval { get; set; } = 2f;

    /// <summary>
    /// Boxes below this height have left the ground and would fall forever, staying awake, so they are removed once
    /// a second and counted as lost.
    /// </summary>
    public float KillHeight { get; set; } = -20f;

    /// <summary>The physics service, absent when the physics bricks are not installed.</summary>
    /// <remarks>The injected setters are protected: injection into a derived type cannot see private ones.</remarks>
    [InjectService(Optional = true), JsonIgnore]
    public PhysicsScene? Physics { get; protected set; }

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; protected set; }

    /// <summary>The session clock, paused by P.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public SimulationClock? Clock { get; protected set; }

    /// <summary>Whether a run started; the scene stays empty until R.</summary>
    [JsonIgnore, Hide]
    public bool Running { get; private set; }

    /// <summary>The boxes spawned so far in this run.</summary>
    [JsonIgnore, Hide]
    public int BoxCount { get; private set; }

    /// <summary>The boxes removed below <see cref="KillHeight"/> in this run.</summary>
    [JsonIgnore, Hide]
    public int LostCount { get; private set; }

    /// <summary>Simulated seconds since the run started.</summary>
    [JsonIgnore, Hide]
    public double Elapsed { get; private set; }

    /// <summary>Simulated seconds from the start until every box slept, or null while boxes move or spawn.</summary>
    [JsonIgnore, Hide]
    public double? SettledSeconds { get; private set; }

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public abstract string Title { get; }

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help => [.. SceneHelp, "R: start or restart", "P: pause"];

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Status
    {
        get
        {
            if (!Running)
            {
                return "Press R to start";
            }

            var state = Spawning ? "spawning " : SettledSeconds is null ? "settling " : "settled after ";
            var paused = Clock?.IsPaused == true ? "  [paused]" : "";
            return string.Create(CultureInfo.InvariantCulture,
                $"{BoxCount} boxes{Detail}, {state}{SettledSeconds ?? Elapsed:0.0} s{paused}");
        }
    }

    /// <summary>The scene's own key lines, shown above the shared ones.</summary>
    protected abstract IReadOnlyList<string> SceneHelp { get; }

    /// <summary>Extra status text after the box count.</summary>
    protected virtual string Detail => "";

    /// <summary>Whether boxes are still being added; the run cannot settle before they stop.</summary>
    protected virtual bool Spawning => false;

    /// <summary>The group the boxes are spawned under.</summary>
    protected Node Bodies { get; private set; } = null!;

    /// <inheritdoc />
    public override void OnStart()
    {
        Bodies = DemoBodies.Group(Node!, "Boxes");
        ResetScene(); // TEMP(screenshots): start without R; revert before commit.
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        HandleInput();
        if (!Running)
        {
            return;
        }

        Elapsed += deltaTime;
        sinceReport += deltaTime;
        Advance(deltaTime);
        sinceSweep += deltaTime;
        if (sinceSweep >= 1)
        {
            sinceSweep = 0;
            RemoveFallen();
        }

        var stats = PhysicsStatsPanel.ReadStats(Physics);
        // New boxes become bodies at the next step, so they only count as settled once they exist and sleep.
        var built = stats.Bodies - stats.StaticBodies >= BoxCount - LostCount;
        if (SettledSeconds is null && !Spawning && BoxCount > 0 && built && stats.AwakeBodies == 0)
        {
            SettledSeconds = Elapsed;
            Report(stats);
        }

        if (ReportInterval > 0 && sinceReport >= ReportInterval)
        {
            Report(stats);
        }
    }

    /// <inheritdoc />
    public void ResetScene()
    {
        DemoBodies.Clear(Bodies);
        BoxCount = LostCount = 0;
        Elapsed = sinceReport = sinceSweep = 0;
        SettledSeconds = null;
        Running = true;
        Begin(new Random(Seed));
        Log.Logger.LogInformation("bench start {Title}: boxes={Count}{Detail} seed={Seed}", Title, BoxCount, Detail,
            Seed);
    }

    /// <summary>Spawns the run's first boxes.</summary>
    protected abstract void Begin(Random random);

    /// <summary>Advances a running scene by a frame of simulated time.</summary>
    protected virtual void Advance(float deltaTime)
    {
    }

    /// <summary>Handles the scene's own keys.</summary>
    protected abstract void HandleSceneInput(IInputSource input);

    /// <summary>Spawns one box of <see cref="BoxSize"/> in a palette colour.</summary>
    protected void SpawnBox(Random random, Vector3 position, Quaternion rotation)
    {
        DemoBodies.Box(Bodies, position, rotation, new Vector3(BoxSize), Palette[random.Next(Palette.Length)]);
        BoxCount++;
    }

    /// <summary>A uniformly random value in [-1, 1].</summary>
    protected static float Next(Random random) => (float)random.NextDouble() * 2f - 1f;

    /// <summary>A random orientation.</summary>
    protected static Quaternion RandomRotation(Random random) =>
        Quaternion.Normalize(new Quaternion(Next(random), Next(random), Next(random), 1f));

    void HandleInput()
    {
        if (Input is null)
        {
            return;
        }

        if (Input.WasKeyPressed(Key.R))
        {
            ResetScene();
        }

        if (Input.WasKeyPressed(Key.P) && Clock is not null)
        {
            Clock.IsPaused = !Clock.IsPaused;
        }

        HandleSceneInput(Input);
    }

    void Report(PhysicsStatistics stats)
    {
        sinceReport = 0;
        var fps = Node!.Children.Select(static child => child.GetComponent<PhysicsStatsPanel>())
            .FirstOrDefault(static panel => panel is not null)?.FramesPerSecond ?? 0;
        Log.Logger.LogInformation("bench {Title} {Stats} lost={Lost} fps={Fps:0} t={Elapsed:0.0}s settled={Settled}",
            Title, PhysicsStatsPanel.FormatStats(stats), LostCount, fps, Elapsed,
            SettledSeconds is { } settled ? settled.ToString("0.0", CultureInfo.InvariantCulture) + "s" : "no");
        if (!Spawning && SettledSeconds is null && stats.AwakeBodies > 0)
        {
            ReportRestless();
        }
    }

    void RemoveFallen()
    {
        foreach (var box in Bodies.Children)
        {
            if (!box.DestroyQueued && box.GlobalTransform.Position.Y < KillHeight)
            {
                box.Destroy();
                LostCount++;
            }
        }
    }

    // One moving body keeps its whole island awake, so a pile that does not settle names the bodies above the sleep
    // threshold: how many, and the three moving fastest.
    void ReportRestless()
    {
        var threshold = Physics?.Settings.SleepThreshold ?? 0.1f;
        var restless = Bodies.Children
            .Select(static node => node.GetComponent<RigidbodyComponent>())
            .Where(body => body is not null && !body.IsSleeping() &&
                body.LinearVelocity.LengthSquared() + body.AngularVelocity.LengthSquared() >= threshold * threshold)
            .Select(static body => body!)
            .OrderByDescending(static body => body.LinearVelocity.LengthSquared() + body.AngularVelocity.LengthSquared())
            .ToList();
        var top = string.Join("; ", restless.Take(3).Select(static body =>
        {
            var position = body.Node!.GlobalTransform.Position;
            return string.Create(CultureInfo.InvariantCulture,
                $"v={body.LinearVelocity.Length():0.000} w={body.AngularVelocity.Length():0.000} at ({position.X:0.0}, {position.Y:0.0}, {position.Z:0.0})");
        }));
        Log.Logger.LogInformation("bench {Title} restless={Restless} {Top}", Title, restless.Count, top);
    }
}
