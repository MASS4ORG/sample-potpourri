namespace Usercode;

/// <summary>A capsule course with low/high steps, walkable/steep slopes and a translating, rotating platform.</summary>
public sealed class ControllerCourse : Component, IDemoScene
{
    Node group = null!;
    RigidbodyComponent platform = null!;
    float elapsed;
    float vertical;
    bool jump;

    /// <summary>The optional game input used by the interactive course.</summary>
    [InjectService(Optional = true), JsonIgnore]
    public IInputSource? Input { get; private set; }

    /// <summary>The capsule moved by the course's controls.</summary>
    [JsonIgnore, Hide]
    public CharacterControllerComponent Character { get; private set; } = null!;

    /// <summary>The currently selected course station.</summary>
    [JsonIgnore, Hide]
    public int Station { get; private set; }

    /// <summary>Whether keyboard input supplies movement; false allows scripted movement.</summary>
    public bool ReadInput { get; set; } = true;

    /// <summary>Horizontal X/Z movement in the range minus one to one.</summary>
    [JsonIgnore, Hide]
    public Vector2 Movement { get; set; }

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Title => "Controller";

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public IReadOnlyList<string> Help =>
    [
        "Arrow keys: move capsule   Space: jump",
        "C: steps / gentle slope / steep slope / platform",
        "R: rebuild course",
        "Step limit 0.30 m: blue 0.25 m, gold 0.65 m",
        "Slope limit 45 degrees: green 25, red 60",
        "Platform carries a stationary capsule, including rotation",
    ];

    /// <inheritdoc/>
    [JsonIgnore, Hide]
    public string Status => $"Station {Station + 1}: {Character?.CollisionFlags}, y={Character?.Node?.Position.Y:0.00} m";

    /// <inheritdoc/>
    public override void OnStart()
    {
        group = DemoBodies.Group(Node!, "Controller course");
        ResetScene();
    }

    /// <inheritdoc/>
    public void ResetScene()
    {
        DemoBodies.Clear(group);
        elapsed = 0;
        Block(new(-6, 0.125f, 3), new(3, 0.25f, 3), "#3e8ed0");
        Block(new(-6, 0.325f, 7), new(3, 0.65f, 3), "#f2b134");
        Ramp(0, 25, "#4fb477");
        Ramp(6, 60, "#e8604c");
        platform = DemoBodies.Box(group, new(0, 1, -5), Quaternion.Identity, new(4, 0.2f, 4),
            Color.FromHex("#9467bd"));
        platform.IsKinematic = true;
        var player = DemoBodies.Group(group, "Capsule player");
        Character = (CharacterControllerComponent)player.AddComponent(new CharacterControllerComponent
        {
            Radius = 0.35f, Height = 1.8f, Center = new(0, 0.9f, 0), StepOffset = 0.3f,
        });
        var visual = DemoBodies.Group(player, "Capsule visual");
        visual.Position = new(0, 0.9f, 0);
        visual.Scale = new(0.7f, 1.8f, 0.7f);
        visual.AddComponent(new ModelComponent { Model = new(DemoBodies.SphereModel), Color = Color.FromHex("#eeeeef") });
        SelectStation(0);
    }

    void Block(Vector3 position, Vector3 size, string color) =>
        DemoBodies.StaticBox(group, position, Quaternion.Identity, size, Color.FromHex(color));

    void Ramp(float x, float degrees, string color)
    {
        var angle = degrees * MathF.PI / 180;
        DemoBodies.StaticBox(group, new(x, 0, 1 + 4 * MathF.Cos(angle)),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, -angle), new(3, 0.2f, 8), Color.FromHex(color));
    }

    /// <summary>Places the character at one of the four stations and resets its motion.</summary>
    public void SelectStation(int station)
    {
        Station = Math.Clamp(station, 0, 3);
        Vector3[] positions = [new(-6, 0.1f, 0), new(0, 0.1f, 0), new(6, 0.1f, 0), new(1, 1.2f, -5)];
        Character.Teleport(positions[Station]);
        Movement = Vector2.Zero;
        vertical = 0;
        jump = false;
    }

    /// <summary>Requests a jump at the next fixed tick when grounded.</summary>
    public void Jump() => jump = true;

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        if (!ReadInput || Input is not { } input)
        {
            return;
        }
        Movement = new((input.IsKeyDown(Key.Right) ? 1 : 0) - (input.IsKeyDown(Key.Left) ? 1 : 0),
            (input.IsKeyDown(Key.Up) ? 1 : 0) - (input.IsKeyDown(Key.Down) ? 1 : 0));
        if (input.WasKeyPressed(Key.Space))
        {
            Jump();
        }
        if (input.WasKeyPressed(Key.C))
        {
            SelectStation((Station + 1) % 4);
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
        platform.MovePosition(new(MathF.Sin(elapsed * 0.6f) * 1.5f, 1, -5));
        platform.MoveRotation(Quaternion.CreateFromAxisAngle(Vector3.UnitY, elapsed * 0.3f));
        vertical = jump && Character.IsGrounded ? 5 : Character.IsGrounded ? -0.2f : vertical - 9.81f * fixedDeltaTime;
        jump = false;
        var move = Movement.LengthSquared() > 1 ? Vector2.Normalize(Movement) : Movement;
        var flags = Character.Move(new Vector3(move.X * 3, vertical, move.Y * 3) * fixedDeltaTime);
        if ((flags & CollisionFlags.Above) != 0)
        {
            vertical = MathF.Min(vertical, 0);
        }
    }
}
