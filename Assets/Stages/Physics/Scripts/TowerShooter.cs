namespace Usercode;

/// <summary>Builds a round tower and a pyramid to knock down: the mouse shoots, drags and blows up bodies.</summary>
public sealed class TowerShooter : Component, IDemoScene
{
    static readonly Color Brick = Color.FromHex("#c8734a");
    static readonly Color BrickAlt = Color.FromHex("#e0a66a");
    static readonly Color Block = Color.FromHex("#6aa4d8");
    static readonly Color Ball = Color.FromHex("#f2e15c");

    readonly Queue<(RigidbodyComponent Body, double Born)> balls = new();
    readonly List<RigidbodyComponent> bricks = [];
    Node structure = null!;
    Node ammo = null!;
    DemoCamera? camera;
    DemoHud? hud;
    SceneTabs? tabs;
    RigidbodyComponent? grabbed;
    float grabDistance;
    Vector3 grabTarget;
    double time;
    double lastShot = double.NegativeInfinity;
    int lastExplosion;

    /// <summary>Layers of bricks in the tower.</summary>
    public int Rings { get; set; } = 24;

    /// <summary>Bricks in each layer.</summary>
    public int BoxesPerRing { get; set; } = 30;

    /// <summary>The tower radius in meters, measured to the brick centers.</summary>
    public float Radius { get; set; } = 5.2f;

    /// <summary>Where the tower stands.</summary>
    public Vector3 TowerCenter { get; set; } = new(6.5f, 0, 0);

    /// <summary>Whether a pyramid of cubes stands beside the tower.</summary>
    public bool BuildPyramid { get; set; } = true;

    /// <summary>Rows in the pyramid.</summary>
    public int PyramidHeight { get; set; } = 18;

    /// <summary>Where the middle of the pyramid's base stands.</summary>
    public Vector3 PyramidCenter { get; set; } = new(-11f, 0, 0);

    /// <summary>The launch speed of a ball in meters per second.</summary>
    public float BallSpeed { get; set; } = 25f;

    /// <summary>The mass of a ball in kilograms; a brick weighs 1.</summary>
    public float BallMass { get; set; } = 5f;

    /// <summary>The ball diameter in meters.</summary>
    public float BallDiameter { get; set; } = 0.6f;

    /// <summary>Seconds between shots while the button is held.</summary>
    public float FireRate { get; set; } = 0.1f;

    /// <summary>Balls alive at once; the oldest disappears first.</summary>
    public int MaxBalls { get; set; } = 64;

    /// <summary>Seconds a ball lives.</summary>
    public float BallLifetime { get; set; } = 10f;

    /// <summary>How fast a dragged body closes the gap to the cursor, per second.</summary>
    public float DragStiffness { get; set; } = 12f;

    /// <summary>The impulse at the center of an explosion, in newton-seconds.</summary>
    public float ExplosionForce { get; set; } = 18f;

    /// <summary>The explosion radius in meters.</summary>
    public float ExplosionRadius { get; set; } = 5f;

    /// <summary>The physics service, absent when the physics bricks are not installed.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public PhysicsScene? Physics { get; private set; }

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The bricks and blocks of the current build.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<RigidbodyComponent> Bricks => bricks;

    /// <summary>The balls currently alive.</summary>
    [JsonIgnore, Hide]
    public int BallCount => balls.Count;

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Title => "Tower shooter";

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Left button: shoot (hold for bursts)",
        "F (hold): drag the body under the cursor",
        "E: explode at the cursor",
        "R: rebuild",
    ];

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Status => string.Create(CultureInfo.InvariantCulture,
        $"{bricks.Count} bricks, balls {balls.Count}/{MaxBalls}, " +
        $"{(grabbed is null ? "nothing dragged" : "dragging " + grabbed.Node?.Name)}, last blast {lastExplosion}");

    /// <inheritdoc />
    public override void OnStart()
    {
        camera = Node!.Children.Select(static child => child.GetComponent<DemoCamera>())
            .FirstOrDefault(static found => found is not null);
        hud = Node.GetComponent<DemoHud>();
        tabs = Node.Children.Select(static child => child.GetComponent<SceneTabs>())
            .FirstOrDefault(static found => found is not null);
        structure = DemoBodies.Group(Node, "Structure");
        ammo = DemoBodies.Group(Node, "Balls");
        ResetScene();
    }

    /// <inheritdoc />
    public void ResetScene()
    {
        EndDrag();
        DemoBodies.Clear(structure);
        DemoBodies.Clear(ammo);
        balls.Clear();
        bricks.Clear();
        BuildTower();
        if (BuildPyramid)
        {
            BuildPyramidWall();
        }
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        time += deltaTime;
        ExpireBalls();
        if (Input is null || CursorRay() is not { } ray)
        {
            return;
        }

        if (Input.IsMouseButtonDown(MouseButton.Left) && !TouchKeys.Covers(Input.MousePosition) &&
            time - lastShot >= FireRate)
        {
            Shoot(ray);
        }

        if (Input.WasKeyPressed(Key.F))
        {
            BeginDrag(ray);
        }
        else if (!Input.IsKeyDown(Key.F))
        {
            EndDrag();
        }

        grabTarget = ray.Origin + ray.Direction * grabDistance;
        if (Input.WasKeyPressed(Key.E) && Physics?.Raycast(ray.Origin, ray.Direction, out var hit, 500f) == true)
        {
            Explode(hit.Point);
        }

        if (Input.WasKeyPressed(Key.R))
        {
            ResetScene();
        }
    }

    /// <inheritdoc />
    public override void OnFixedUpdate(float fixedDeltaTime)
    {
        if (grabbed is null)
        {
            return;
        }

        // A velocity spring: closing the gap at a fixed rate stays stable at any mass, unlike a force spring.
        var velocity = (grabTarget - grabbed.Position) * DragStiffness;
        grabbed.LinearVelocity = velocity.Length() > 30f ? Vector3.Normalize(velocity) * 30f : velocity;
        grabbed.AngularVelocity *= 0.9f;
        grabbed.WakeUp();
    }

    /// <summary>Launches a ball along a ray; continuous collision keeps it from tunnelling through bricks.</summary>
    public RigidbodyComponent Shoot(Ray ray)
    {
        lastShot = time;
        var ball = DemoBodies.Sphere(ammo, ray.Origin + ray.Direction * 1.5f, BallDiameter, Ball, BallMass);
        ball.CollisionDetectionMode = CollisionDetectionMode.Continuous;
        ball.LinearVelocity = ray.Direction * BallSpeed;
        balls.Enqueue((ball, time));
        while (balls.Count > MaxBalls)
        {
            Remove(balls.Dequeue().Body);
        }

        return ball;
    }

    /// <summary>Grabs the body under a ray, if any; returns whether one was grabbed.</summary>
    public bool BeginDrag(Ray ray)
    {
        if (Physics?.Raycast(ray.Origin, ray.Direction, out var hit, 500f) != true ||
            hit.Rigidbody is not { IsKinematic: false } body)
        {
            return false;
        }

        grabbed = body;
        grabDistance = hit.Distance;
        grabTarget = hit.Point;
        return true;
    }

    /// <summary>Moves the dragged body toward a point until <see cref="EndDrag"/>.</summary>
    public void DragTo(Vector3 point) => grabTarget = point;

    /// <summary>Releases the dragged body.</summary>
    public void EndDrag() => grabbed = null;

    /// <summary>Pushes every body near a point outward and up; returns how many bodies were pushed.</summary>
    public int Explode(Vector3 point)
    {
        var pushed = new HashSet<RigidbodyComponent>();
        foreach (var collider in Physics?.OverlapSphere(point, ExplosionRadius) ?? [])
        {
            if (collider.AttachedRigidbody is { IsKinematic: false } body && pushed.Add(body))
            {
                body.WakeUp();
                body.AddExplosionForce(ExplosionForce, point, ExplosionRadius, 1f, ForceMode.Impulse);
            }
        }

        lastExplosion = pushed.Count;
        return pushed.Count;
    }

    void BuildTower()
    {
        var brick = new Vector3(1f, 0.5f, 0.5f);
        var step = 2f * MathF.PI / BoxesPerRing;
        for (var ring = 0; ring < Rings; ring++)
        {
            var offset = ring % 2 * step * 0.5f;
            for (var i = 0; i < BoxesPerRing; i++)
            {
                var angle = i * step + offset;
                var position = TowerCenter + new Vector3(MathF.Cos(angle) * Radius, brick.Y * (ring + 0.5f),
                    MathF.Sin(angle) * Radius);
                // Turns the brick's long X axis along the ring's tangent.
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -(angle + MathF.PI / 2f));
                bricks.Add(DemoBodies.Box(structure, position, rotation, brick, ring % 2 == 0 ? Brick : BrickAlt));
            }
        }
    }

    void BuildPyramidWall()
    {
        for (var row = 0; row < PyramidHeight; row++)
        {
            var count = PyramidHeight - row;
            for (var i = 0; i < count; i++)
            {
                var position = PyramidCenter + new Vector3((i - (count - 1) * 0.5f) * 1.02f, row + 0.5f, 0);
                bricks.Add(DemoBodies.Box(structure, position, Quaternion.Identity, Vector3.One, Block));
            }
        }
    }

    void ExpireBalls()
    {
        while (balls.TryPeek(out var oldest) && time - oldest.Born > BallLifetime)
        {
            Remove(balls.Dequeue().Body);
        }
    }

    void Remove(RigidbodyComponent ball)
    {
        if (ReferenceEquals(grabbed, ball))
        {
            EndDrag();
        }

        if (ball.Node is { } node)
        {
            DemoBodies.Despawn(node);
        }
    }

    Ray? CursorRay()
    {
        if (camera is null || Input is null)
        {
            return null;
        }

        // Before the overlay's first layout the viewport size is unknown; shoot straight ahead until then.
        var viewport = hud?.ViewportSize ?? Vector2.Zero;
        return camera.ScreenRay(TouchKeys.Aim ?? Input.MousePosition, viewport) ??
               new Ray(camera.Camera.Position, Vector3.Normalize(camera.Camera.Front));
    }
}
