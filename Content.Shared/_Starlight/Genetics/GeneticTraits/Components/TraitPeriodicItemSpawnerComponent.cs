using System.Linq;
using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics.GeneticTraits.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class TraitPeriodicItemSpawnerComponent : Component
{
    /// <summary>
    /// The time when the associated item collection is spawned. Once exceeded, the associated item collection is spawned and the timespan is incremented by the associated cooldown.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField, AutoPausedField]
    public Dictionary<ProtoId<SpawnCollectionPrototype>, TimeSpan> WhenNextSpawns = new();

    /// <summary>
    /// What collection to spawn. Note that the prototype is different from the instance. Usually the instance is scaled up by some factor.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<ProtoId<SpawnCollectionPrototype>, SpawnCollectionInstance> SpawnCollectionInstances = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpawnCollectionInstance
{
    public List<SCPEntry> Items;
    public TimeSpan Cooldown;

    public SpawnCollectionInstance(List<SCPEntry> items, TimeSpan cooldown)
    {
        Items = items;
        Cooldown = cooldown;
    }

    public SpawnCollectionInstance(SpawnCollectionPrototype proto, int multiplier) : this(proto.Items, proto.Cooldown)
    {
        for (var i = 0; i < Items.Count; i++) Items[i] = new(Items[i].Id, Items[i].Amount * multiplier);
    }

    public static SpawnCollectionInstance operator +(SpawnCollectionInstance left, SpawnCollectionInstance right) => new(left.Items.Concat(right.Items).ToList(), left.Cooldown);
}
