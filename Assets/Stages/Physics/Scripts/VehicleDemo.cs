namespace Usercode;

/// <summary>A four-wheel raycast vehicle with suspension, torque, braking and front-wheel steering.</summary>
public sealed class VehicleDemo : Component, IDemoScene
{
    Node group = null!;
    readonly List<WheelColliderComponent> wheels = [];
    readonly List<Node> visuals = [];

    /// <summary>The optional controls supplied by the player.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The vehicle's 400 kg chassis.</summary>
    [JsonIgnore, Hide]
    public RigidbodyComponent Chassis { get; private set; } = null!;

    /// <summary>The four suspension sensors, with the front pair first.</summary>
    [JsonIgnore, Hide]
    public IReadOnlyList<WheelColliderComponent> Wheels => wheels;

    /// <summary>Whether keyboard input supplies driving commands.</summary>
    public bool ReadInput { get; set; } = true;

    /// <summary>Engine command between reverse at minus one and forward at one.</summary>
    [JsonIgnore, Hide]
    public float Throttle { get; set; }

    /// <summary>Steering command between left at minus one and right at one.</summary>
    [JsonIgnore, Hide]
    public float Steering { get; set; }

    /// <summary>Whether the brakes are applied to every wheel.</summary>
    [JsonIgnore, Hide]
    public bool Braking { get; set; }

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Title => "Vehicle";

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Up / Down: throttle / reverse",
        "Left / Right: steer   Space: brake   R: reset",
        "Four raycast wheels support a 400 kg chassis",
        "Wheel meshes follow suspension, steering and spin",
        "Drive forward to the green ramp and blue bumps",
    ];

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Status => $"Speed {Chassis?.LinearVelocity.Length():0.0} m/s   " +
        $"Grounded {wheels.Count(static wheel => wheel.IsGrounded)}/4   RPM {wheels.FirstOrDefault()?.Rpm:0}";

    /// <inheritdoc/>
    public override void OnStart()
    {
        group = DemoBodies.Group(Node!, "Vehicle course");
        ResetScene();
    }

    /// <inheritdoc/>
    public void ResetScene()
    {
        DemoBodies.Clear(group);
        wheels.Clear();
        visuals.Clear();
        Throttle = Steering = 0;
        Braking = false;
        var car = DemoBodies.Group(group, "Raycast vehicle");
        car.Position = new(0, 1, -6);
        Chassis = (RigidbodyComponent)car.AddComponent(new RigidbodyComponent { Mass = 400 });
        car.AddComponent(new BoxColliderComponent { Size = new(1.6f, 0.3f, 3) });
        var shell = DemoBodies.Group(car, "Chassis visual");
        shell.Scale = new(1.6f, 0.3f, 3);
        shell.AddComponent(new ModelComponent { Model = new(DemoBodies.CubeModel), Color = Color.FromHex("#e8604c") });
        foreach (var (x, z) in new[] { (-0.8f, 1.2f), (0.8f, 1.2f), (-0.8f, -1.2f), (0.8f, -1.2f) })
        {
            var mount = DemoBodies.Group(car, $"Wheel {wheels.Count + 1}");
            mount.Position = new(x, 0, z);
            var wheel = (WheelColliderComponent)mount.AddComponent(new WheelColliderComponent
            {
                Radius = 0.4f, SuspensionDistance = 0.3f,
            });
            wheels.Add(wheel);
            var visual = DemoBodies.Group(group, $"Tyre {visuals.Count + 1}");
            visual.AddComponent(new ModelComponent { Model = new(DemoBodies.SphereModel), Color = Color.FromHex("#252a33") });
            visuals.Add(visual);
        }
        DemoBodies.StaticBox(group, new(0, 0.35f, 15), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.12f),
            new(5, 0.3f, 7), Color.FromHex("#4fb477"));
        for (var i = 0; i < 3; i++)
        {
            DemoBodies.StaticBox(group, new(0, 0.06f, 24 + i * 2), Quaternion.Identity,
                new(5, 0.12f, 0.35f), Color.FromHex("#3e8ed0"));
        }
    }

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (!ReadInput || Input is not { } input)
        {
            return;
        }
        Throttle = (input.IsKeyDown(Key.Up) ? 1 : 0) - (input.IsKeyDown(Key.Down) ? 1 : 0);
        Steering = (input.IsKeyDown(Key.Right) ? 1 : 0) - (input.IsKeyDown(Key.Left) ? 1 : 0);
        Braking = input.IsKeyDown(Key.Space);
        if (input.WasKeyPressed(Key.R))
        {
            ResetScene();
        }
    }

    /// <inheritdoc/>
    public override void OnFixedUpdate(float fixedDeltaTime)
    {
        for (var i = 0; i < wheels.Count; i++)
        {
            wheels[i].MotorTorque = Math.Clamp(Throttle, -1, 1) * 150;
            wheels[i].BrakeTorque = Braking ? 1500 : 0;
            wheels[i].SteerAngle = i < 2 ? Math.Clamp(Steering, -1, 1) * 20 : 0;
        }
    }

    /// <inheritdoc/>
    public override void OnLateUpdate(float deltaTime)
    {
        for (var i = 0; i < wheels.Count; i++)
        {
            wheels[i].GetWorldPose(out var position, out var rotation);
            visuals[i].GlobalTransform = new Transform { Position = position, Orientation = rotation, Scale = new(0.25f, 0.8f, 0.8f) };
        }
    }
}
