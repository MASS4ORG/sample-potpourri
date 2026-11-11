namespace Usercode;

/// <summary>Rains boxes at a steady rate up to a user-chosen total, to find where the frame rate breaks.</summary>
public sealed class BoxRainBenchmark : BenchmarkScene
{
    Random rain = new();
    double pending;
    double verificationTimeout;
    double? rainEnded;
    bool verificationFinished;

    /// <summary>The run stops spawning at this many boxes.</summary>
    public int MaxBoxes { get; set; } = 10_000;

    /// <summary>Boxes spawned per simulated second.</summary>
    public float SpawnRate { get; set; } = 200f;

    /// <summary>The height boxes appear at, in meters.</summary>
    public float SpawnHeight { get; set; } = 30f;

    /// <summary>The side of the square, centered on the origin, that boxes fall onto.</summary>
    public float Area { get; set; } = 24f;

    /// <inheritdoc />
    public override string Title => "Box rain";

    /// <inheritdoc />
    protected override IReadOnlyList<string> SceneHelp =>
    [
        string.Create(CultureInfo.InvariantCulture, $"+ / -: double or halve the total ({MaxBoxes})"),
        string.Create(CultureInfo.InvariantCulture, $"[ / ]: halve or double the rate ({SpawnRate:0}/s)"),
    ];

    /// <inheritdoc />
    protected override string Detail => string.Create(CultureInfo.InvariantCulture, $" of {MaxBoxes}");

    /// <inheritdoc />
    protected override bool Spawning => BoxCount < MaxBoxes;

    /// <inheritdoc />
    public override void OnStart()
    {
        base.OnStart();
        if (double.TryParse(Environment.GetEnvironmentVariable("TURIAN_SETTLE_TIMEOUT"),
                CultureInfo.InvariantCulture, out verificationTimeout) && double.IsFinite(verificationTimeout) &&
            verificationTimeout > 0)
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("TURIAN_SETTLE_BOXES"), out var count) && count > 0)
            {
                MaxBoxes = count;
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("TURIAN_SETTLE_SEED"), out var seed))
            {
                Seed = seed;
            }

            ResetScene();
        }
        else
        {
            verificationTimeout = 0;
        }
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        if (verificationTimeout <= 0 || !Running || verificationFinished || Spawning)
        {
            return;
        }

        rainEnded ??= Elapsed;
        var stats = PhysicsStatsPanel.ReadStats(Physics);
        var passed = SettledSeconds is { } settled && settled - rainEnded.Value <= verificationTimeout &&
            stats.AwakeBodies == 0 && LostCount * 100L <= BoxCount;
        if (!passed && Elapsed - rainEnded.Value < verificationTimeout)
        {
            return;
        }

        // Keep rendering for two simulated seconds after sleep so the last report includes the asleep path.
        if (passed && Elapsed - SettledSeconds!.Value < 2)
        {
            return;
        }

        verificationFinished = true;
        Log.Logger.LogInformation(
            "settle-result passed={Passed} seed={Seed} boxes={Boxes} lost={Lost} after-rain={Seconds:0.000}s timeout={Timeout:0.000}s {Stats}",
            passed, Seed, BoxCount, LostCount, (SettledSeconds ?? Elapsed) - rainEnded.Value,
            verificationTimeout, PhysicsStatsPanel.FormatStats(stats));
        Application.Quit(passed ? 0 : 1);
    }

    /// <inheritdoc />
    protected override void Begin(Random random)
    {
        rain = random;
        pending = 0;
        rainEnded = null;
        verificationFinished = false;
    }

    /// <inheritdoc />
    protected override void Advance(float deltaTime)
    {
        pending += SpawnRate * deltaTime;
        while (pending >= 1 && BoxCount < MaxBoxes)
        {
            pending--;
            var position = new Vector3(Next(rain) * Area * 0.5f, SpawnHeight + (float)rain.NextDouble() * 4f,
                Next(rain) * Area * 0.5f);
            SpawnBox(rain, position, RandomRotation(rain));
        }

        if (BoxCount >= MaxBoxes)
        {
            pending = 0;
        }
    }

    /// <inheritdoc />
    protected override void HandleSceneInput(IInputSource input)
    {
        if (input.WasKeyPressed(Key.Equal) || input.WasKeyPressed(Key.KeypadAdd))
        {
            MaxBoxes = (int)Math.Min(1_000_000L, MaxBoxes * 2L);
        }

        if (input.WasKeyPressed(Key.Minus) || input.WasKeyPressed(Key.KeypadSubtract))
        {
            MaxBoxes = Math.Max(100, MaxBoxes / 2);
        }

        if (input.WasKeyPressed(Key.RightBracket))
        {
            SpawnRate = MathF.Min(5000f, SpawnRate * 2f);
        }

        if (input.WasKeyPressed(Key.LeftBracket))
        {
            SpawnRate = MathF.Max(10f, SpawnRate * 0.5f);
        }
    }
}
