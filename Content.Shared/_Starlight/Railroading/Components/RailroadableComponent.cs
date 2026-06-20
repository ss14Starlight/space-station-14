using Content.Shared._Starlight.Abstract;

namespace Content.Shared._Starlight.Railroading.Components;

[RegisterComponent]
public sealed partial class RailroadableComponent : Component
{
    [ViewVariables]
    [NonSerialized]
    public List<Entity<RailroadCardComponent, RuleOwnerComponent>>? IssuedCards;

    [ViewVariables]
    [NonSerialized]
    public Entity<RailroadCardComponent, RuleOwnerComponent>? ActiveCard;

    [ViewVariables]
    [NonSerialized]
    public List<Entity<RailroadCardComponent, RuleOwnerComponent>>? Completed;

    [DataField]
    [NonSerialized]
    public bool Restricted = false;

    [DataField, NonSerialized] // ? What exactly does NonSerialized means and... is it really needed here?
    public bool Important = false;
}
