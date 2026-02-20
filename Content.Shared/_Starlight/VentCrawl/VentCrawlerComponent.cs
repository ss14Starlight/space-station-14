using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.VentCrawl;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VentCrawlerComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool InTube = false;

    public float EnterDelay = 2.5f;

    // Starlight Start
    [DataField]
    public bool MayCarryItems = true;
    //Starlight End
}


[Serializable, NetSerializable]
public sealed partial class EnterVentDoAfterEvent : SimpleDoAfterEvent
{
}
