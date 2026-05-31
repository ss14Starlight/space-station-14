namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
/// Marks an entity as consumable by vampires via UseInHand
/// </summary>
[RegisterComponent]
public sealed partial class VampireDevourableComponent : Component
{
    [DataField]
    public TimeSpan DevourDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How much <see cref="VampireComponent.BloodFullness"/> is restored when consumed
    /// </summary>
    [DataField]
    public float BloodFullnessRestore = 25f;
}
