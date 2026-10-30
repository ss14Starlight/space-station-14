using Content.Shared.Fax.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Fax;

[RegisterComponent]
public sealed partial class SendFaxRuleComponent : Component
{
    [DataField] public bool EnqueueFaxes;
    [DataField] public List<SendFaxRuleData> Faxes = [];
    [ViewVariables] public TimeSpan? NextFaxTime;
    [ViewVariables] public Queue<SendFaxRuleData> QueuedFaxes = [];
}

[DataDefinition]
public sealed partial class SendFaxRuleData
{
    /// <summary>
    /// If specified, the prototype to fax. Must have <see cref="FaxableObjectComponent"/>.
    /// </summary>
    [DataField] public EntProtoId? FaxPrototype;

    /// <summary>
    /// If specified, data gets merged with the paper component of either
    /// <see cref="FaxPrototype"/>, or a blank paper if said field is left unspecified.
    /// </summary>
    [DataField] public PaperData? PaperData;

    /// <summary>
    /// List of faxes to send to.
    /// </summary>
    [DataField] public HashSet<string> Addresses = [];

    /// <summary>
    /// Only valid if <see cref="SendFaxRuleComponent.EnqueueFaxes"/> is true.
    /// Determines the delay after rule start or previous fax at which this fax will be sent, in seconds.
    /// </summary>
    [DataField] public float? Delay;
}
