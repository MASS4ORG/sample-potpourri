namespace Usercode;

/// <summary>Six independent exhibits demonstrate fixed, motorized hinge, ball socket, slider, distance and spring joints.</summary>
public sealed class JointGallery : Component, IDemoScene
{
    Node group = null!;
    readonly List<JointComponent> joints = [];
    readonly List<RigidbodyComponent> bodies = [];

    /// <summary>The optional player controls.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The six exhibits in fixed/hinge/socket/slider/distance/spring order.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<JointComponent> Joints => joints;

    /// <summary>The body moved by each exhibit's joint.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<RigidbodyComponent> Bodies => bodies;

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Title => "Joints";

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Left to right: fixed, hinge, socket, slider, rope, spring",
        "M: toggle hinge / slider motors   J: release / attach",
        "R: rebuild exhibits",
        "Hinge motor 45 deg/s, limits -30 to +60 degrees",
        "Slider travel 0 to 1.2 m; rope max 2 m",
        "Select a joint with Scene Gizmos for anchors and axes",
    ];

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Status => $"Attached {joints.Count(static joint => joint.IsActive)}/6   " +
        $"Motors {(joints.OfType<HingeJointComponent>().FirstOrDefault()?.UseMotor == true ? "on" : "off")}";

    /// <inheritdoc/>
    public override void OnStart()
    {
        group = DemoBodies.Group(Node!, "Joint exhibits");
        ResetScene();
    }

    /// <inheritdoc/>
    public void ResetScene()
    {
        DemoBodies.Clear(group);
        joints.Clear();
        bodies.Clear();
        Add(-10, 2, new FixedJointComponent());
        Add(-6, 2, new HingeJointComponent
        {
            Axis = Vector3.UnitY, Anchor = new(-0.5f, 0, 0), UseMotor = true, MotorSpeed = 45, MotorForce = 100,
            UseLimits = true, MinAngle = -30, MaxAngle = 60,
        });
        Add(-2, 2, new BallSocketJointComponent
        {
            Anchor = new(0, 0.6f, 0), UseSwingLimit = true, SwingLimit = 45,
        }).LinearVelocity = Vector3.UnitX;
        Add(2, 2, new SliderJointComponent
        {
            Axis = Vector3.UnitY, UseMotor = true, MotorSpeed = 0.6f, MotorForce = 100,
            UseLimits = true, MinDistance = 0, MaxDistance = 1.2f,
        });
        Add(6, 1.5f, new DistanceJointComponent
        {
            AutoConfigureConnectedAnchor = false, ConnectedAnchor = new(6, 3.5f, 0), MaxDistance = 2,
        }).LinearVelocity = Vector3.UnitX;
        Add(10, 1.5f, new SpringJointComponent
        {
            AutoConfigureConnectedAnchor = false, ConnectedAnchor = new(10, 3.5f, 0), RestDistance = 1.7f,
            Frequency = 3, DampingRatio = 0.5f,
        });
    }

    RigidbodyComponent Add(float x, float y, JointComponent joint)
    {
        var color = Color.FromHex(joints.Count % 2 == 0 ? "#3e8ed0" : "#f2b134");
        var body = DemoBodies.Box(group, new(x, y, 0), Quaternion.Identity, new(0.7f), color);
        var anchor = DemoBodies.Group(group, $"Anchor {joints.Count + 1}");
        anchor.Position = joint.AutoConfigureConnectedAnchor
            ? Vector3.Transform(joint.Anchor, body.Node!.GlobalTransform.Matrix4X4())
            : joint.ConnectedAnchor;
        anchor.Scale = new(0.15f);
        anchor.AddComponent(new ModelComponent { Model = new(DemoBodies.SphereModel), Color = Color.White });
        body.Node!.AddComponent(joint);
        joints.Add(joint);
        bodies.Add(body);
        return body;
    }

    /// <summary>Enables or releases every joint without rebuilding the gallery.</summary>
    public void SetAttached(bool attached)
    {
        foreach (var joint in joints)
        {
            joint.IsActive = attached;
        }
    }

    /// <summary>Enables or disables the hinge and slider motors.</summary>
    public void SetMotors(bool enabled)
    {
        ((HingeJointComponent)joints[1]).UseMotor = enabled;
        ((SliderJointComponent)joints[3]).UseMotor = enabled;
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (Input is not { } input)
        {
            return;
        }
        if (input.WasKeyPressed(Key.M))
        {
            SetMotors(!((HingeJointComponent)joints[1]).UseMotor);
        }
        if (input.WasKeyPressed(Key.J))
        {
            SetAttached(!joints[0].IsActive);
        }
        if (input.WasKeyPressed(Key.R))
        {
            ResetScene();
        }
    }
}
