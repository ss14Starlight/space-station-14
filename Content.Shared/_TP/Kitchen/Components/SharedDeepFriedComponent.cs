using Robust.Shared.GameStates;

namespace Content.Shared._TP.Kitchen.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharedDeepFriedComponent : Component
{
    [DataField, AutoNetworkedField]
    public FriedLevel CurrentFriedLevel = FriedLevel.None;

    public enum FriedLevel
    {
        LightlyFried,
        Fried,
        Burnt,
        None,
    }
}
