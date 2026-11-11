namespace Usercode;

/// <summary>Counts each callback and keeps the latest contact data for the physics lab's HUD and smoke checks.</summary>
public sealed class PhysicsEventProbe : Component, ICollisionHandler, ITriggerHandler
{
    /// <summary>Collision enter, stay and exit callbacks received by this body.</summary>
    [JsonIgnore, Hide]
    public (int Enter, int Stay, int Exit) Collisions { get; private set; }

    /// <summary>Trigger enter, stay and exit callbacks received by this body.</summary>
    [JsonIgnore, Hide]
    public (int Enter, int Stay, int Exit) Triggers { get; private set; }

    /// <summary>The latest collision contact, absent until the body touches a solid.</summary>
    [JsonIgnore, Hide]
    public ContactPoint? Contact { get; private set; }

    /// <inheritdoc />
    public void OnCollisionEnter(Collision collision)
    {
        Collisions = Collisions with { Enter = Collisions.Enter + 1 };
        Remember(collision);
    }

    /// <inheritdoc />
    public void OnCollisionStay(Collision collision)
    {
        Collisions = Collisions with { Stay = Collisions.Stay + 1 };
        Remember(collision);
    }

    /// <inheritdoc />
    public void OnCollisionExit(Collision collision) => Collisions = Collisions with { Exit = Collisions.Exit + 1 };

    /// <inheritdoc />
    public void OnTriggerEnter(ColliderComponent other)
    {
        Triggers = Triggers with { Enter = Triggers.Enter + 1 };
        Tint(Color.FromHex("#f2b134"));
    }

    /// <inheritdoc />
    public void OnTriggerStay(ColliderComponent other) => Triggers = Triggers with { Stay = Triggers.Stay + 1 };

    /// <inheritdoc />
    public void OnTriggerExit(ColliderComponent other)
    {
        Triggers = Triggers with { Exit = Triggers.Exit + 1 };
        Tint(Color.FromHex("#3e8ed0"));
    }

    void Remember(Collision collision)
    {
        if (collision.ContactCount > 0)
        {
            Contact = collision.GetContact(0);
        }
    }

    void Tint(Color color)
    {
        if (Node?.GetComponent<ModelComponent>() is { } model)
        {
            model.Color = color;
        }
    }
}
