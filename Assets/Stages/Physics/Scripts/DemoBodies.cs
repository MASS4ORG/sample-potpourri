namespace Usercode;

/// <summary>Spawns and despawns runtime bodies: a node with a model, a collider and, if dynamic, a rigidbody.</summary>
public static class DemoBodies
{
    /// <summary>The unit cube model in <c>Assets/Models/Cube.gltf</c>.</summary>
    public static readonly Guid CubeModel = new("7afdff86-a39d-487b-a0ce-466a9950e7f8");

    /// <summary>The unit-diameter sphere model in <c>Assets/Models/Sphere.gltf</c>.</summary>
    public static readonly Guid SphereModel = new("3a7378e1-edaa-46f0-9d0f-c78282e16c4c");

    /// <summary>Adds an empty node under <paramref name="parent"/> to group runtime bodies.</summary>
    public static Node Group(Node parent, string name) => Attach(parent, new Node { Name = name });

    /// <summary>Spawns a dynamic box; <paramref name="size"/> scales the unit cube and its collider.</summary>
    public static RigidbodyComponent Box(Node parent, Vector3 position, Quaternion rotation, Vector3 size, Color color,
        float mass = 1f, PhysicsMaterial? material = null)
    {
        var node = Spawn(parent, "Box", position, rotation, size, CubeModel, color);
        var body = new RigidbodyComponent { Mass = mass };
        node.AddComponent(body);
        node.AddComponent(new BoxColliderComponent { Material = material });
        return body;
    }

    /// <summary>Spawns a dynamic sphere of the given diameter.</summary>
    public static RigidbodyComponent Sphere(Node parent, Vector3 position, float diameter, Color color,
        float mass = 1f, PhysicsMaterial? material = null)
    {
        var node = Spawn(parent, "Sphere", position, Quaternion.Identity, new Vector3(diameter), SphereModel, color);
        var body = new RigidbodyComponent { Mass = mass };
        node.AddComponent(body);
        node.AddComponent(new SphereColliderComponent { Material = material });
        return body;
    }

    /// <summary>Spawns a static box, such as a ramp, that never moves.</summary>
    public static BoxColliderComponent StaticBox(Node parent, Vector3 position, Quaternion rotation, Vector3 size,
        Color color, PhysicsMaterial? material = null)
    {
        var node = Spawn(parent, "Static box", position, rotation, size, CubeModel, color);
        return (BoxColliderComponent)node.AddComponent(new BoxColliderComponent { Material = material });
    }

    /// <summary>
    /// Removes a spawned node at the end of the frame; destroying its components unregisters them from physics.
    /// </summary>
    public static void Despawn(Node node) => node.Destroy();

    /// <summary>
    /// Despawns every child of a group at the end of the frame, keeping the group. Destroyed siblings leave their parent
    /// in one pass, so clearing thousands of boxes stays linear.
    /// </summary>
    public static void Clear(Node group)
    {
        foreach (var child in group.Children)
        {
            child.Destroy();
        }
    }

    static Node Spawn(Node parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Guid model,
        Color color)
    {
        var node = Attach(parent, new Node
        {
            Name = name,
            Transform = new Transform { Position = position, Orientation = rotation, Scale = scale },
        });
        node.AddComponent(new ModelComponent { Model = new AssetReference<ModelAsset>(model), Color = color });
        return node;
    }

    // Awake binds the scene's services before components are added, so each one is injected and enabled once.
    static Node Attach(Node parent, Node node)
    {
        parent.Children.Add(node);
        node.Awake(parent);
        return node;
    }
}
