using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

[RegisterComponent]
public sealed partial class ChangelingRegenerationActionComponent : Component
{
    [DataField] public float HealingPerSecond = 4;
    [DataField] public float ChemicalCostPerSecond = 5;
    [DataField] public float EvolvedHealingPerSecond = 8;
    [DataField] public float EvolvedChemicalCostPerSecond = 3;
}

[RegisterComponent]
public sealed partial class ChangelingShockHandComponent : Component
{
    [DataField] public float ChemicalCost = 10;
    [DataField] public TimeSpan DischargeCooldown = TimeSpan.FromSeconds(30);
    [DataField] public TimeSpan DischargeDuration = TimeSpan.FromSeconds(3);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingPainSuppressionComponent : Component
{
    [DataField] public float ChemicalCostPerSecond = 2;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingEyesComponent : Component
{
    public bool AddedNight;
    public bool AddedThermal;
    public bool ThermalConfigured;
    public bool NightWasActive;
    public bool ThermalWasActive;
}
