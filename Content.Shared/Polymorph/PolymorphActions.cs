using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Polymorph;

public sealed partial class PolymorphActionEvent : InstantActionEvent
{
    /// <summary>
    ///     The polymorph proto id, containing all the information about
    ///     the specific polymorph.
    /// </summary>
    [DataField]
    public ProtoId<PolymorphPrototype>? ProtoId;

    public PolymorphActionEvent(ProtoId<PolymorphPrototype> protoId) : this()
    {
        ProtoId = protoId;
    }
}

public sealed partial class PolymorphConfigActionEvent : InstantActionEvent
{
    public PolymorphConfiguration Config;

    public PolymorphConfigActionEvent(PolymorphConfiguration config) => Config = config;
}
//Starlight end

public sealed partial class RevertPolymorphActionEvent : InstantActionEvent
{

}
