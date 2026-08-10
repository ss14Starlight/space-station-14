using Robust.Shared.GameObjects;

namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicRiftHealthComponent : Component
{
    [DataField]
    public int AppliedCorpseBonus = 0;

    [DataField]
    public int HealthPerCorpse = 5;
}
