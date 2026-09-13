using Content.Shared.Random;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Ninja;

/// <summary>
/// Component for hacking a pod console to extract.
/// Can only be done once, the ninja is removed afterwards.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedPodHackerSystem))]
public sealed partial class PodHackerComponent : Component
{
    /// <summary>
    /// Time taken to hack the console
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Delay = TimeSpan.FromSeconds(20);
}
