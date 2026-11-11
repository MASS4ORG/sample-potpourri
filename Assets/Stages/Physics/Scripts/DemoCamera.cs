namespace Usercode;

/// <summary>Orbits the camera around a target: right drag turns, the wheel zooms, WASD moves the target.</summary>
public sealed class DemoCamera : Component
{
    long lastUpdate = Stopwatch.GetTimestamp();

    /// <summary>The point the camera looks at.</summary>
    public Vector3 Target { get; set; } = new(0, 2, 0);

    /// <summary>The distance from the target in meters.</summary>
    public float Distance { get; set; } = 20f;

    /// <summary>The heading around the target in degrees.</summary>
    public float Yaw { get; set; } = 30f;

    /// <summary>The angle above the horizon in degrees.</summary>
    public float Pitch { get; set; } = 25f;

    /// <summary>The player input, absent in headless hosts.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The camera this component steers.</summary>
    [JsonIgnore, Hide]
    public CameraComponent Camera => Node!.GetComponent<CameraComponent>()!;

    /// <summary>The world ray under a viewport point, or null while the viewport size is unknown.</summary>
    public Ray? ScreenRay(Vector2 point, Vector2 viewport) => CameraMath.ScreenPointToRay(Camera, point, viewport);

    /// <inheritdoc />
    public override void OnAwake() => Apply();

    /// <inheritdoc />
    public override void OnUpdate(float deltaTime)
    {
        // A scene unloaded during this frame's updates still runs its remaining components once, detached.
        if (Input is null || Node is null)
        {
            return;
        }

        if (Input.IsMouseButtonDown(MouseButton.Right))
        {
            Yaw += Input.MouseDelta.X * 0.3f;
            Pitch = Math.Clamp(Pitch + Input.MouseDelta.Y * 0.3f, -10f, 85f);
        }

        Distance = Math.Clamp(Distance * MathF.Pow(0.9f, Input.MouseScrollDelta), 3f, 150f);
        ApplyTouch(Input);
        var heading = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Yaw * MathF.PI / 180f);
        var forward = Vector3.Transform(Vector3.UnitZ, heading);
        // The camera looks along +Z, so screen right is its -X.
        var right = Vector3.Transform(-Vector3.UnitX, heading);
        var move = Axis(Key.W, Key.S) * forward + Axis(Key.D, Key.A) * right;
        // Real time, so the camera still moves while the simulation is paused.
        Target += move * Distance * 0.6f * (float)Math.Min(Stopwatch.GetElapsedTime(lastUpdate).TotalSeconds, 0.1);
        lastUpdate = Stopwatch.GetTimestamp();
        Apply();
    }

    // One finger turns the view, two also pinch to zoom. The tower shooter keeps one finger for shooting.
    void ApplyTouch(IInputSource input)
    {
        var fingers = TouchKeys.SceneFingers(input);
        if (fingers.Count >= 2)
        {
            var before = Vector2.Distance(fingers[0].Position - fingers[0].Delta, fingers[1].Position - fingers[1].Delta);
            var now = Vector2.Distance(fingers[0].Position, fingers[1].Position);
            if (before > 1f && now > 1f)
            {
                Distance = Math.Clamp(Distance * before / now, 3f, 150f);
            }

            Turn((fingers[0].Delta + fingers[1].Delta) * 0.5f);
        }
        else if (fingers.Count == 1 && Node!.Parent?.GetComponent<TowerShooter>() is null)
        {
            Turn(fingers[0].Delta);
        }
    }

    void Turn(Vector2 delta)
    {
        Yaw += delta.X * 0.2f;
        Pitch = Math.Clamp(Pitch + delta.Y * 0.2f, -10f, 85f);
    }

    float Axis(Key positive, Key negative) =>
        (Input!.IsKeyDown(positive) ? 1f : 0f) - (Input.IsKeyDown(negative) ? 1f : 0f);

    void Apply()
    {
        var orientation = Quaternion.CreateFromYawPitchRoll(Yaw * MathF.PI / 180f, Pitch * MathF.PI / 180f, 0f);
        var forward = Vector3.Transform(Vector3.UnitZ, orientation);
        Node!.Transform = new Transform
        {
            Position = Target - forward * Distance,
            Orientation = orientation,
            Scale = Vector3.One,
        };
    }
}
