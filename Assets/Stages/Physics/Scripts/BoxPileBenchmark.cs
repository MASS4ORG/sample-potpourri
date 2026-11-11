namespace Usercode;

/// <summary>How the box-pile benchmark arranges its boxes before they fall.</summary>
public enum BenchmarkLayout
{
    /// <summary>A jittered grid of randomly rotated boxes dropped onto the ground.</summary>
    Grid,

    /// <summary>Upright stacks on a sunflower spiral, already resting.</summary>
    Stacks,
}

/// <summary>Drops a configurable pile of boxes at once and measures how long it takes to come to rest.</summary>
public sealed class BoxPileBenchmark : BenchmarkScene
{
    /// <summary>Boxes along X.</summary>
    public int CountX { get; set; } = 10;

    /// <summary>Boxes along Y, the number of layers or the stack height.</summary>
    public int CountY { get; set; } = 10;

    /// <summary>Boxes along Z.</summary>
    public int CountZ { get; set; } = 10;

    /// <summary>The distance between neighbouring boxes, as a multiple of <see cref="BenchmarkScene.BoxSize"/>.</summary>
    public float Spacing { get; set; } = 1.2f;

    /// <summary>The random offset of each grid box, as a multiple of <see cref="BenchmarkScene.BoxSize"/>.</summary>
    public float Jitter { get; set; } = 0.1f;

    /// <summary>The height of the lowest grid layer in meters.</summary>
    public float DropHeight { get; set; } = 5f;

    /// <summary>The arrangement of the boxes.</summary>
    public BenchmarkLayout Layout { get; set; } = BenchmarkLayout.Grid;

    /// <inheritdoc />
    public override string Title => "Box pile";

    /// <inheritdoc />
    protected override IReadOnlyList<string> SceneHelp =>
    [
        string.Create(CultureInfo.InvariantCulture, $"+ / -: more or fewer boxes ({CountX * CountY * CountZ})"),
        $"L: switch layout ({Layout})",
    ];

    /// <inheritdoc />
    protected override string Detail => ", " + Layout;

    /// <inheritdoc />
    protected override void Begin(Random random)
    {
        if (Layout == BenchmarkLayout.Grid)
        {
            SpawnGrid(random);
        }
        else
        {
            SpawnStacks(random);
        }
    }

    /// <inheritdoc />
    protected override void HandleSceneInput(IInputSource input)
    {
        if (input.WasKeyPressed(Key.Equal) || input.WasKeyPressed(Key.KeypadAdd))
        {
            Resize(2);
        }

        if (input.WasKeyPressed(Key.Minus) || input.WasKeyPressed(Key.KeypadSubtract))
        {
            Resize(-2);
        }

        if (input.WasKeyPressed(Key.L))
        {
            Layout = Layout == BenchmarkLayout.Grid ? BenchmarkLayout.Stacks : BenchmarkLayout.Grid;
            Restart();
        }
    }

    void Resize(int step)
    {
        CountX = Math.Max(1, CountX + step);
        CountZ = Math.Max(1, CountZ + step);
        Restart();
    }

    // Settings changes apply to a run in progress; before the first run they only update the help.
    void Restart()
    {
        if (Running)
        {
            ResetScene();
        }
    }

    void SpawnGrid(Random random)
    {
        var pitch = BoxSize * Spacing;
        var origin = new Vector3((CountX - 1) * pitch * -0.5f, DropHeight, (CountZ - 1) * pitch * -0.5f);
        for (var y = 0; y < CountY; y++)
        {
            for (var z = 0; z < CountZ; z++)
            {
                for (var x = 0; x < CountX; x++)
                {
                    var jitter = new Vector3(Next(random), Next(random), Next(random)) * Jitter * BoxSize;
                    SpawnBox(random, origin + new Vector3(x, y, z) * pitch + jitter, RandomRotation(random));
                }
            }
        }
    }

    void SpawnStacks(Random random)
    {
        var goldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));
        for (var i = 0; i < CountX * CountZ; i++)
        {
            var radius = BoxSize * Spacing * 1.2f * MathF.Sqrt(i);
            var angle = i * goldenAngle;
            var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)random.NextDouble() * MathF.PI);
            for (var y = 0; y < CountY; y++)
            {
                var position = new Vector3(MathF.Cos(angle) * radius, BoxSize * (y + 0.5f) + 0.01f * y,
                    MathF.Sin(angle) * radius);
                SpawnBox(random, position, yaw);
            }
        }
    }
}
