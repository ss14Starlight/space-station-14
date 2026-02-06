using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenobiologyConsoleComponent : Component
{
    /// <summary>
    /// Tag for monkeys. Required in order to determine what is a monkey.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<TagPrototype> MonkeyTag = default!;
    
    /// <summary>
    /// Tag for monkey cubes. Required in order to determine what is a monkey cube.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<TagPrototype> MonkeyCubeTag = default!;

    /// <summary>
    /// The id name of the monkey prototype to spawn.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId MonkeyProtoId = default!;
    
    /// <summary>
    /// The name of the container the slime corpses are stored in.
    /// </summary>
    public const string SlimeContainerName = "slimes";
    
    /// <summary>
    /// Container for slimes stored in the console.
    /// Slimes don't "pause" while in the console, so they will get hungry and might even die.
    /// Act fast!
    /// </summary>
    [ViewVariables]
    public ContainerSlot SlimeContainer = default!;

    /// <summary>
    /// The amount of monkey cubes currently stored in the console.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 MonkeyCubes = FixedPoint2.Zero;

    /// <summary>
    /// The amount of mutation-increasing potions stored in the console.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int MutationPotions = 0;

    /// <summary>
    /// The amount of mutation-decreasing potions stored in the console.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int StabilizerPotions = 0;
}