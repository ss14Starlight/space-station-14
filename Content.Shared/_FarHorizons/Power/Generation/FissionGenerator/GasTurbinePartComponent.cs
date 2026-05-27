using Content.Shared._FarHorizons.Materials;
using Content.Shared.Guidebook;
using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Power.Generation.FissionGenerator;

public abstract partial class GasTurbinePartComponent : Component
{
    [Dependency] private IPrototypeManager _proto = default!;

    [DataField("material")]
    public ProtoId<MaterialPrototype> Material = "Steel";

    public MaterialProperties Properties
    {
        get
        {
            IoCManager.Resolve(ref _proto);
            _properties ??= new MaterialProperties(_proto.Index(Material).Properties);

            return _properties;
        }
        set => _properties = value;
    }
#pragma warning disable IDE0032 // Despite what the IDE insists, making this an auto property causes it to explode
    [DataField("properties")]
    private MaterialProperties? _properties;
#pragma warning restore IDE0032
}

[RegisterComponent, NetworkedComponent]
public sealed partial class GasTurbineBladeComponent : GasTurbinePartComponent
{
    [GuidebookData]
    public float GuidebookIntegrity => Math.Max(1, 5 * Properties.Hardness);

    [GuidebookData]
    public float GuidebookInertia => Math.Max(200, 200 * Properties.Density);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class GasTurbineStatorComponent : GasTurbinePartComponent
{
    [GuidebookData]
    public float GuidebookEfficiency => Math.Max(0.2f, 0.2f * Properties.ElectricalConductivity);
}
