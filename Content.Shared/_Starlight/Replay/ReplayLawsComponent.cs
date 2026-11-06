using Content.Shared._Starlight.Thaven;
using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Replay;

/// <summary>
/// Copy of an entity's silicon laws or shared thaven moods for replay viewers. Laws otherwise only reach clients when
/// the silicon opens its laws window. Session-specific and refused to every real player, so only replays receive it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ReplayLawsComponent : Component
{
    public override bool SessionSpecific => true;

    [AutoNetworkedField]
    public List<SiliconLaw>? SiliconLaws;

    [AutoNetworkedField]
    public List<ThavenMood>? SharedMoods;
}
