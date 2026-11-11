namespace Usercode;

/// <summary>
/// Drops spheres and slides boxes made of physic materials copied from Unity sample projects, on Unity's default
/// ground material, so friction, bounciness and their combine modes can be compared side by side.
/// </summary>
public sealed class MaterialsShowcase : Component, IDemoScene
{
    // Name, dynamic friction, static friction, bounciness, friction combine, bounce combine; values as authored.
    static readonly (string Name, float Dynamic, float Static, float Bounce, PhysicsMaterialCombine Friction,
        PhysicsMaterialCombine Combine)[] Library =
        [
            ("ZeroFriction", 0f, 0f, 0f, PhysicsMaterialCombine.Multiply, PhysicsMaterialCombine.Average),
            ("Walls", 0.6f, 0.6f, 0f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average),
            ("Player", 0.3f, 0.5f, 0.2f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Multiply),
            ("Icosphere", 0.4f, 0.5f, 0.8f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Multiply),
            ("Ball", 0.4f, 0.4f, 1f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average),
            ("Grenade", 20f, 20f, 0.6f, PhysicsMaterialCombine.Maximum, PhysicsMaterialCombine.Maximum),
        ];

    static readonly string[] SphereMaterials = ["Walls", "Player", "Icosphere", "Ball", "Grenade"];
    static readonly string[] BoxMaterials = ["ZeroFriction", "Player", "Icosphere", "Walls", "Grenade"];
    static readonly Color Blue = Color.FromHex("#3e8ed0");
    static readonly Color Red = Color.FromHex("#e8604c");

    readonly List<RigidbodyComponent> spheres = [];
    readonly List<RigidbodyComponent> boxes = [];
    readonly float[] peaks = new float[SphereMaterials.Length];
    readonly bool[] bounced = new bool[SphereMaterials.Length];
    Node bodies = null!;

    /// <summary>The height the spheres drop from, in meters.</summary>
    public float DropHeight { get; set; } = 6f;

    /// <summary>The ramp's slope in degrees; boxes with friction below its tangent slide.</summary>
    public float RampAngle { get; set; } = 20f;

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The dropped spheres, in <see cref="Help"/> order.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<RigidbodyComponent> Spheres => spheres;

    /// <summary>The ramp boxes, in <see cref="Help"/> order.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<RigidbodyComponent> Boxes => boxes;

    /// <summary>Each sphere's highest point after its first bounce, in meters.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<float> BouncePeaks => peaks;

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Title => "Materials";

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Spheres, left to right (bounciness, combine):",
        "Walls 0 · Player 0.2 Multiply · Icosphere 0.8 Multiply",
        "Ball 1 Average · Grenade 0.6 Maximum",
        "On the default ground (0, Average): 0, 0, 0, 0.5, 0.6",
        "Ramp boxes, front to back (friction):",
        "ZeroFriction 0 · Player 0.3 · Icosphere 0.4",
        "Walls 0.6 · Grenade 20; below tan 20° = 0.36 they slide",
        "R: drop everything again",
    ];

    /// <inheritdoc />
    [JsonIgnore, Hide]
    public string Status => "bounce peaks: " + string.Join("  ",
        peaks.Select((peak, i) => bounced[i] ? peak.ToString("0.00", CultureInfo.InvariantCulture) : "-"));

    /// <inheritdoc />
    public override void OnStart()
    {
        bodies = DemoBodies.Group(Node!, "Bodies");
        ResetScene();
    }

    /// <inheritdoc />
    public void ResetScene()
    {
        DemoBodies.Clear(bodies);
        spheres.Clear();
        boxes.Clear();
        Array.Clear(peaks);
        Array.Clear(bounced);
        for (var i = 0; i < SphereMaterials.Length; i++)
        {
            var (material, bounce) = Material(SphereMaterials[i]);
            // The camera looks along +Z, so world +X is screen left.
            var position = new Vector3(4f - 2f * i, DropHeight, -1f);
            spheres.Add(DemoBodies.Sphere(bodies, position, 1f, Color.Lerp(Blue, Red, bounce), 1f, material));
        }

        BuildRamp();
    }

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        if (Input?.WasKeyPressed(Key.R) == true)
        {
            ResetScene();
        }
    }

    /// <inheritdoc />
    public override void OnFixedUpdate(float fixedDeltaTime)
    {
        for (var i = 0; i < spheres.Count; i++)
        {
            var sphere = spheres[i];
            if (sphere.LinearVelocity.Y > 0.05f)
            {
                bounced[i] = true;
            }

            // The sphere's center rests at 0.5 m, so the peak is measured from there.
            if (bounced[i])
            {
                peaks[i] = MathF.Max(peaks[i], sphere.Position.Y - 0.5f);
            }
        }
    }

    void BuildRamp()
    {
        var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, RampAngle * MathF.PI / 180f);
        var center = new Vector3(0f, 2.6f, 6f);
        var size = new Vector3(12f, 0.5f, 7.5f);
        // No material: Unity's default, as on the ground.
        DemoBodies.StaticBox(bodies, center, tilt, size, Color.FromHex("#8a8f98"));
        for (var i = 0; i < BoxMaterials.Length; i++)
        {
            var (material, _) = Material(BoxMaterials[i]);
            // Near the high end, resting on the ramp's top face.
            var local = new Vector3(4f, size.Y * 0.5f + 0.3f + 0.01f, -2.8f + 1.4f * i);
            var color = Color.Lerp(Blue, Red, MathF.Min(material.DynamicFriction, 1f));
            boxes.Add(DemoBodies.Box(bodies, center + Vector3.Transform(local, tilt), tilt, new Vector3(0.6f), color,
                1f, material));
        }
    }

    static (PhysicsMaterial Material, float Bounce) Material(string name)
    {
        var entry = Library.Single(entry => entry.Name == name);
        var material = new PhysicsMaterial
        {
            DynamicFriction = entry.Dynamic,
            StaticFriction = entry.Static,
            Bounciness = entry.Bounce,
            FrictionCombine = entry.Friction,
            BounceCombine = entry.Combine,
        };
        return (material, entry.Bounce);
    }
}
