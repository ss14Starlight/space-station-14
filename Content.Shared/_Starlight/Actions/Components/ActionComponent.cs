namespace Content.Shared.Actions.Components;
public sealed partial class ActionComponent : Component
{
    /// <summary>
    ///     If not null, this action will cost x amount of hunger points per use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? MetabolicCost;
}
