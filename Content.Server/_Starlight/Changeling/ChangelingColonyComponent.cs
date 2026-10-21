namespace Content.Server._Starlight.Changeling;

/// <summary>The colony belongs to the mind; only its active carrier owns changeling resources.</summary>
[RegisterComponent]
public sealed partial class ChangelingColonyComponent : Component
{
    public EntityUid Active;
    public bool Learned;
    public HashSet<EntityUid> Members = new();
    public bool AnimateBlood;
    public bool Evolved;
    public bool Moving;
    public bool SuppressAnimation;
    public Dictionary<EntityUid, bool> DisabledActions = new();
    public TimeSpan NextUpdate;
}

[RegisterComponent]
public sealed partial class ChangelingCaptureComponent : Component
{
    public EntityUid Attacker;
}
