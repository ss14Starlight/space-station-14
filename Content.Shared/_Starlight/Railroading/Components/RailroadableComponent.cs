using Content.Shared._Starlight.Abstract;

namespace Content.Shared._Starlight.Railroading.Components;

[RegisterComponent]
public sealed partial class RailroadableComponent : Component
{
    [ViewVariables]
    public List<Entity<RailroadCardComponent, RuleOwnerComponent>>? IssuedCards;

    [ViewVariables]
    public Entity<RailroadCardComponent, RuleOwnerComponent>? ActiveCard;

    [ViewVariables]
    public List<Entity<RailroadCardComponent, RuleOwnerComponent>>? Completed;

    [DataField]
    public bool Restricted = false;

    [DataField]
    public bool Important = false;
}
