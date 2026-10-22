using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

[RegisterComponent]
public sealed partial class ChangelingRegenerationActionComponent : Component
{
    [DataField] public float HealingPerSecond = 4;
    [DataField] public float ChemicalCostPerSecond = 5;
}

[RegisterComponent]
public sealed partial class ChangelingShockHandComponent : Component
{
    [DataField] public float ChemicalCost = 10;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingPainSuppressionComponent : Component;
