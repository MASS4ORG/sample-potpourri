namespace Usercode;

/// <summary>Fits a posed bone hierarchy to a ragdoll and switches between animation and simulation.</summary>
public sealed class RagdollDemo : Component, IDemoScene
{
    Node group = null!;
    Node model = null!;
    float elapsed;

    /// <summary>The optional player controls.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The generated rig, exposing the original bones and their physics proxies.</summary>
    [JsonIgnore, Hide]
    public RagdollRig Rig { get; private set; } = null!;

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Title => "Ragdoll";

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Space: animated pose / ragdoll   I: impulse   R: reset",
        "Eleven selected bones, ten limited socket joints",
        "Capsules fit bone endpoints; total mass 70 kg",
        "Physics proxies drive the original bone hierarchy",
        "Scene Gizmos show capsule shapes and joint anchors",
    ];

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Status => $"{(Rig?.IsKinematic != false ? "Animation" : "Physics")}   " +
        $"Parts {Rig?.Parts.Count ?? 0}   Hips y={Rig?.Parts.FirstOrDefault()?.Bone.GlobalTransform.Position.Y:0.00} m";

    /// <inheritdoc/>
    public override void OnStart()
    {
        group = DemoBodies.Group(Node!, "Ragdoll demonstration");
        ResetScene();
    }

    /// <inheritdoc/>
    public void ResetScene()
    {
        Rig?.Dispose();
        DemoBodies.Clear(group);
        elapsed = 0;
        model = Bone(group, "Skeleton", new(0, 3, 0));
        var definitions = new List<RagdollBone>();
        var hips = Bone(model, "Hips", Vector3.Zero);
        var chest = Bone(hips, "Chest", new(0, 0.45f, 0));
        var head = Bone(chest, "Head", new(0, 0.55f, 0));
        var crown = Bone(head, "Crown endpoint", new(0, 0.35f, 0));
        definitions.Add(new() { Bone = hips, EndBone = chest, MassWeight = 8, Radius = 0.16f });
        definitions.Add(new() { Bone = chest, EndBone = head, MassWeight = 10, Radius = 0.18f });
        definitions.Add(new() { Bone = head, EndBone = crown, MassWeight = 4, Radius = 0.14f });
        foreach (var side in new[] { -1f, 1f })
        {
            var arm = Bone(chest, $"Arm {side}", new(side * 0.22f, 0.3f, 0));
            var forearm = Bone(arm, $"Forearm {side}", new(side * 0.5f, 0, 0));
            var hand = Bone(forearm, $"Hand endpoint {side}", new(side * 0.4f, 0, 0));
            definitions.Add(new() { Bone = arm, EndBone = forearm, MassWeight = 2, Radius = 0.08f });
            definitions.Add(new() { Bone = forearm, EndBone = hand, MassWeight = 1, Radius = 0.07f });
            var thigh = Bone(hips, $"Thigh {side}", new(side * 0.2f, -0.15f, 0));
            var shin = Bone(thigh, $"Shin {side}", new(0, -0.65f, 0));
            var foot = Bone(shin, $"Foot endpoint {side}", new(0, -0.6f, 0));
            definitions.Add(new() { Bone = thigh, EndBone = shin, MassWeight = 5, Radius = 0.1f });
            definitions.Add(new() { Bone = shin, EndBone = foot, MassWeight = 3, Radius = 0.09f });
        }
        Rig = RagdollBuilder.Create(model, definitions, new() { TotalMass = 70, SwingLimit = 50 });
        foreach (var part in Rig.Parts)
        {
            var visual = DemoBodies.Group(part.Bone, "Bone visual");
            visual.GlobalTransform = part.Collider.Node!.GlobalTransform with
            {
                Scale = new(part.Collider.Radius * 2, part.Collider.Height, part.Collider.Radius * 2),
            };
            visual.AddComponent(new ModelComponent
            {
                Model = new(DemoBodies.CubeModel),
                Color = Color.FromHex(part.Joint is null ? "#f2b134" : "#3e8ed0"),
            });
        }
    }

    static Node Bone(Node parent, string name, Vector3 position)
    {
        var node = DemoBodies.Group(parent, name);
        node.Position = position;
        return node;
    }

    /// <summary>Switches modes while keeping the current posed skeleton as the transition pose.</summary>
    public void SetDynamic(bool dynamic) => Rig.SetKinematic(!dynamic);

    /// <summary>Pushes the hips forward when physics is active.</summary>
    public void Impulse()
    {
        if (!Rig.IsKinematic)
        {
            Rig.Parts[0].Body.AddForce(new(0, 0, 20), ForceMode.Impulse);
        }
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (Input is not { } input)
        {
            return;
        }
        if (input.WasKeyPressed(Key.Space))
        {
            SetDynamic(Rig.IsKinematic);
        }
        if (input.WasKeyPressed(Key.I))
        {
            Impulse();
        }
        if (input.WasKeyPressed(Key.R))
        {
            ResetScene();
        }
    }

    /// <inheritdoc/>
    public override void OnFixedUpdate(float fixedDeltaTime)
    {
        elapsed += fixedDeltaTime;
        if (Rig.IsKinematic)
        {
            model.Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(elapsed) * 0.15f);
            Rig.SyncFromBones();
        }
    }

    /// <inheritdoc/>
    public override void OnLateUpdate(float deltaTime) => Rig.SyncToBones();

    /// <inheritdoc/>
    public override void OnDestroy() => Rig?.Dispose();
}
