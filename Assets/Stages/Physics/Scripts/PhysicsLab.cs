namespace Usercode;

/// <summary>Exercises query shapes, trigger callbacks, layer filtering, mesh contacts and a fast CCD projectile.</summary>
public sealed class PhysicsLab : Component, IDemoScene
{
    static readonly Vector3 QueryOrigin = new(4, 6, 0);
    static readonly Vector3 QueryCenter = new(4, 2, 0);
    static readonly string[] Shapes = ["Ray / sphere overlap", "Sphere", "Box", "Capsule"];
    static readonly Color Blue = Color.FromHex("#3e8ed0");
    static readonly Color Gold = Color.FromHex("#f2b134");
    readonly RaycastHit[] rayHits = new RaycastHit[8];
    Node bodies = null!;
    RigidbodyComponent shuttle = null!;
    Node marker = null!;
    float elapsed;
    bool launched;

    /// <summary>The running physics scene, absent in hosts without the physics bricks.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public PhysicsScene? Physics { get; private set; }

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The query shape selected by Q: ray, sphere, box or capsule.</summary>
    public int ShapeIndex { get; set; }

    /// <summary>Whether queries include the upper gold trigger target.</summary>
    public bool HitTriggers { get; set; } = true;

    /// <summary>Whether queries only select the lower blue target's layer.</summary>
    public bool OnlySolidLayer { get; set; }

    /// <summary>The callbacks received by the sphere moving through the trigger frame.</summary>
    [JsonIgnore, Hide]
    public PhysicsEventProbe TriggerProbe { get; private set; } = null!;

    /// <summary>The callbacks and contact normal of the falling sphere on the left.</summary>
    [JsonIgnore, Hide]
    public PhysicsEventProbe CollisionProbe { get; private set; } = null!;

    /// <summary>The ball resting on the raised triangle mesh platform.</summary>
    [JsonIgnore, Hide]
    public RigidbodyComponent MeshBall { get; private set; } = null!;

    /// <summary>The falling convex hull on the left rear.</summary>
    [JsonIgnore, Hide]
    public RigidbodyComponent Hull { get; private set; } = null!;

    /// <summary>The fast projectile aimed at the rear wall.</summary>
    [JsonIgnore, Hide]
    public RigidbodyComponent Bullet { get; private set; } = null!;

    /// <summary>The number of distinct ray hits under the selected mask and trigger policy.</summary>
    [JsonIgnore, Hide]
    public int RayHitCount { get; private set; }

    /// <summary>The number of colliders inside the selected overlap volume.</summary>
    [JsonIgnore, Hide]
    public int OverlapCount { get; private set; }

    /// <summary>The nearest hit of the selected cast, absent when the query misses.</summary>
    [JsonIgnore, Hide]
    public RaycastHit? CastHit { get; private set; }

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Title => "Physics lab";

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Q: ray / sphere / box / capsule cast + overlap",
        "T: query triggers   L: only solid layer   R: reset",
        "M: toggle the moving sphere's trigger collision",
        "Blue sphere crosses the gold frame; orange inside",
        "Rear: convex hull, mesh platform, CCD wall",
        "Scene view Gizmos: collider wires and contact normals",
        $"Query: {Shapes[Math.Clamp(ShapeIndex, 0, 3)]}",
        $"Layers: {(OnlySolidLayer ? "7" : "7 + 9")}  Triggers: {HitTriggers}",
        $"Ray hits: {RayHitCount}  Overlaps: {OverlapCount}",
        $"Trigger enter/stay/exit: {TriggerProbe?.Triggers}",
        $"Collision enter/stay/exit: {CollisionProbe?.Collisions}",
    ];

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Status => CastHit is { } hit
        ? string.Create(CultureInfo.InvariantCulture, $"{hit.Node?.Name}: {hit.Distance:0.00} m") : "No cast hit";

    /// <inheritdoc />
    public override void OnStart()
    {
        bodies = DemoBodies.Group(Node!, "Lab bodies");
        ResetScene();
    }

    /// <inheritdoc />
    public void ResetScene()
    {
        DemoBodies.Clear(bodies);
        elapsed = 0;
        launched = false;
        CastHit = null;
        RayHitCount = OverlapCount = 0;
        BuildTrigger();
        var ball = DemoBodies.Sphere(bodies, new Vector3(-6, 4, 0), 1, Blue);
        CollisionProbe = (PhysicsEventProbe)ball.Node!.AddComponent(new PhysicsEventProbe());
        BuildTargets();
        BuildGeometry();
        BuildBullet();
    }

    /// <inheritdoc />
    public override void OnFixedUpdate(float fixedDeltaTime)
    {
        elapsed += fixedDeltaTime;
        shuttle.MovePosition(new Vector3(-4 * MathF.Cos(elapsed * 1.5f), 1, 0));
        if (!launched && Physics?.BodyCount > 0)
        {
            Bullet.LinearVelocity = new Vector3(0, 0, 400);
            launched = true;
        }
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        if (Input?.WasKeyPressed(Key.R) == true)
        {
            ResetScene();
        }

        if (Input?.WasKeyPressed(Key.Q) == true)
        {
            ShapeIndex = (ShapeIndex + 1) % 4;
        }

        if (Input?.WasKeyPressed(Key.T) == true)
        {
            HitTriggers = !HitTriggers;
        }

        if (Input?.WasKeyPressed(Key.L) == true)
        {
            OnlySolidLayer = !OnlySolidLayer;
        }

        if (Input?.WasKeyPressed(Key.M) == true)
        {
            SetTriggerCollision(shuttle.Node!.PhysicsLayer != 6);
        }

        Query();
    }

    /// <summary>Changes the moving sphere's layer so it either meets or ignores the trigger frame.</summary>
    public void SetTriggerCollision(bool enabled) => shuttle.Node!.PhysicsLayer = enabled ? 6 : 8;

    void BuildTrigger()
    {
        var zone = DemoBodies.StaticBox(bodies, new Vector3(0, 1, 0), Quaternion.Identity, new Vector3(2), Gold);
        zone.IsTrigger = true;
        zone.ExcludeLayers = LayerMask.FromLayer(8);
        zone.Node!.Name = "Trigger zone";
        // Keep the volume visible as an open frame, so the moving sphere is visible while inside.
        zone.Node.GetComponent<ModelComponent>()!.IsActive = false;
        foreach (var x in new[] { -1f, 1f })
        {
            var post = DemoBodies.StaticBox(bodies, new Vector3(x, 1, -1), Quaternion.Identity,
                new Vector3(0.08f, 2, 0.08f), Gold);
            post.IsActive = false;
        }

        var lintel = DemoBodies.StaticBox(bodies, new Vector3(0, 2, -1), Quaternion.Identity,
            new Vector3(2, 0.08f, 0.08f), Gold);
        lintel.IsActive = false;
        shuttle = DemoBodies.Sphere(bodies, new Vector3(-4, 1, 0), 0.6f, Blue);
        shuttle.UseGravity = false;
        SetTriggerCollision(true);
        TriggerProbe = (PhysicsEventProbe)shuttle.Node!.AddComponent(new PhysicsEventProbe());
    }

    void BuildTargets()
    {
        var solid = DemoBodies.StaticBox(bodies, new Vector3(4, 1, 0), Quaternion.Identity, Vector3.One, Blue);
        solid.Node!.Name = "Solid target (layer 7)";
        solid.Node.PhysicsLayer = 7;
        var trigger = DemoBodies.StaticBox(bodies, new Vector3(4, 3, 0), Quaternion.Identity, Vector3.One, Gold);
        trigger.Node!.Name = "Trigger target (layer 9)";
        trigger.Node.PhysicsLayer = 9;
        trigger.IsTrigger = true;
        var pin = DemoBodies.Sphere(bodies, QueryOrigin, 0.15f, Color.FromHex("#e8604c"));
        pin.Node!.GetComponent<SphereColliderComponent>()!.IsActive = false;
        pin.IsActive = false;
        marker = pin.Node;
    }

    void BuildGeometry()
    {
        var platform = DemoBodies.StaticBox(bodies, new Vector3(6, 1, 4), Quaternion.Identity,
            new Vector3(3, 0.3f, 3), Color.FromHex("#4fb477"));
        platform.IsActive = false;
        var mesh = new MeshColliderComponent();
        mesh.SetGeometry([new(-0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, 0.5f),
            new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f)], [0, 1, 2, 0, 2, 3]);
        platform.Node!.AddComponent(mesh);
        MeshBall = DemoBodies.Sphere(bodies, new Vector3(6, 4, 4), 1, Blue);
        Hull = DemoBodies.Box(bodies, new Vector3(-6, 4, 4), Quaternion.Identity, Vector3.One, Gold);
        Hull.Node!.GetComponent<BoxColliderComponent>()!.IsActive = false;
        var convex = new MeshColliderComponent { Convex = true };
        convex.SetGeometry([new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, 0.5f),
            new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, -0.5f),
            new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f)]);
        Hull.Node.AddComponent(convex);
    }

    void BuildBullet()
    {
        DemoBodies.StaticBox(bodies, new Vector3(0, 2, 7), Quaternion.Identity, new Vector3(3, 3, 0.2f), Blue);
        Bullet = DemoBodies.Sphere(bodies, new Vector3(0, 2, 4), 0.2f, Gold);
        Bullet.UseGravity = false;
        Bullet.CollisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Query()
    {
        if (Physics is not { } physics)
        {
            return;
        }

        var mask = OnlySolidLayer ? LayerMask.FromLayer(7) : LayerMask.FromLayer(7) | LayerMask.FromLayer(9);
        var triggers = HitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        RayHitCount = physics.RaycastNonAlloc(QueryOrigin, -Vector3.UnitY, rayHits, 10, mask, triggers);
        RaycastHit hit;
        var found = ShapeIndex switch
        {
            1 => physics.SphereCast(QueryOrigin, 0.2f, -Vector3.UnitY, out hit, 10, mask, triggers),
            2 => physics.BoxCast(QueryOrigin, new Vector3(0.2f), -Vector3.UnitY, out hit, Quaternion.Identity, 10,
                mask, triggers),
            3 => physics.CapsuleCast(QueryOrigin, QueryOrigin + Vector3.UnitY, 0.2f, -Vector3.UnitY, out hit,
                10, mask, triggers),
            _ => physics.Raycast(QueryOrigin, -Vector3.UnitY, out hit, 10, mask, triggers),
        };
        CastHit = found ? hit : null;
        marker.Position = found ? hit.Point : QueryOrigin;
        var overlaps = ShapeIndex switch
        {
            2 => physics.OverlapBox(QueryCenter, new Vector3(1.6f), Quaternion.Identity, mask, triggers),
            3 => physics.OverlapCapsule(new Vector3(4, 1, 0), new Vector3(4, 3, 0), 0.2f, mask, triggers),
            _ => physics.OverlapSphere(QueryCenter, 1.6f, mask, triggers),
        };
        OverlapCount = overlaps.Length;
    }
}
