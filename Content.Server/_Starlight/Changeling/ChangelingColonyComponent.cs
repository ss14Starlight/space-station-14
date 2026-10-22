namespace Content.Server._Starlight.Changeling;

/// <summary>The original mind owns the colony, its shared resources and its absorbed minds.</summary>
[RegisterComponent]
public sealed partial class ChangelingColonyComponent : Component
{
    public EntityUid Active;
    public EntityUid Core;
    public Dictionary<EntityUid, EntityUid> Minds = new();
    public List<EntityUid> Identities = new();
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
