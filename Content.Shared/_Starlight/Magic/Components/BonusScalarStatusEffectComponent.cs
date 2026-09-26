using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Factory for adding <see cref="BonusScalarComponent"/>s to mobs. These are used to manipulate various attributes temporarily by applying coefficients.
/// Multiple effects may be stacked provided they have different entities as sources.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBonusScalarSystem))]
public sealed partial class BonusScalarStatusEffectComponent : Component
{
    [DataField(required: true)]
    public BonusScalarCoefficients coefficients;

    [DataField]
    public bool OverwriteOnRefresh = false;
}
