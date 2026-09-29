using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Lathe;

/// <summary>
/// This is used for linking lathes to a consumer
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LatheLinkingComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid? LinkedEntity;

    [DataField] public ProtoId<SourcePortPrototype> SourcePort = "LatheSender";

    [DataField] public ProtoId<SinkPortPrototype> SinkPort = "LatheReceiver";

    [DataField] public bool Ejecting = true;

}
