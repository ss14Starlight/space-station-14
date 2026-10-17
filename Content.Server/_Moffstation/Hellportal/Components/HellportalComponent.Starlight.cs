namespace Content.Server._Moffstation.Hellportal.Components;

public sealed partial class HellportalComponent : Component
{
    /// <summary>
    /// Mobs allowed per connected player, rounded up. Null uses the fixed MaxSpawns limit.
    /// </summary>
    [DataField]
    public float? MobsPerPlayer;
}
