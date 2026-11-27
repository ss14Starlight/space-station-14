// ReSharper disable CheckNamespace
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.Salvage.JobBoard;

public sealed partial class SalvageJobBoardSystem : EntitySystem
{
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    /// <summary>
    /// Prototype for the stack that is spawned when a salvage job is completed.
    /// </summary>
    private static readonly ProtoId<StackPrototype> TicketStackPrototype = "SalvageTicket";

    /// <summary>
    /// Radio channel that unlock messages are broadcast on.
    /// </summary>
    private static readonly ProtoId<RadioChannelPrototype> UnlockChannel = "Expedition";
}
