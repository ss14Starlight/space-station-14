using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical;

/// <summary>
/// Applied to items that can be converted into humanoids for surgery (torso items)
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ConvertableToMobComponent : Component
{
    [DataField]
    public float ConvertDelay = 3.0f;

    /// <summary>
    /// The mob (torso-only) that gets produced when converted
    /// </summary>
    [DataField]
    public string OutputMob = "MobHuman";
}