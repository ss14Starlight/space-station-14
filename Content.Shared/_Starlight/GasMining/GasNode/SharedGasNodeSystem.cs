using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;

namespace Content.Shared._Starlight.GasMining.GasNode;

public abstract partial class SharedGasNodeSystem : EntitySystem
{
    [Dependency] SharedAtmosphereSystem _atmosphereSystem = default!;

    public float NodeHeatCapacity(Entity<GasNodeComponent> entity)
    {
        var gasNode = entity.Comp;

        if (gasNode.Gases.Count == 0)
            return 0f;

        var totalHeatCapacity = 0f;
        var totalMoles = 0f;

        foreach (var gas in gasNode.Gases)
        {
            totalHeatCapacity += _atmosphereSystem.GasMolarHeatCapacities[(int)gas.Key] * gas.Value;
            totalMoles += gas.Value;
        }

        return totalHeatCapacity / totalMoles;
    }

    public GasMixture TransferGas(Entity<GasNodeComponent> entity, float moles)
    {
        var totalMoles = 0f;
        foreach (var gas in entity.Comp.Gases.Values)
        {
            totalMoles += gas;
        }

        var transferMixture = new GasMixture()
        {
            Temperature = entity.Comp.Temperature
        };

        foreach (var gas in entity.Comp.Gases)
        {
            var portion = moles * (gas.Value / totalMoles);
            portion = float.Clamp(portion, 0f, entity.Comp.Gases[gas.Key]);
            entity.Comp.Gases[gas.Key] -= portion;
            transferMixture.AdjustMoles(gas.Key, portion);
        }

        return transferMixture;
    }

    public void ReturnGas(Entity<GasNodeComponent> entity, GasMixture gasMixture)
    {
        foreach (var gas in gasMixture)
        {
            entity.Comp.Gases[gas.gas] += gas.moles;
        }
    }
}
