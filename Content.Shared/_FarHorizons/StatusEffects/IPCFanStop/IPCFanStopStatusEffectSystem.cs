using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.StatusEffects.IPCFanStop;

public abstract partial class SharedIPCFanStopStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IPCFanStopStatusEffectComponent, StatusEffectAppliedEvent>(OnEffectApplied);
        SubscribeLocalEvent<IPCFanStopStatusEffectComponent, StatusEffectRemovedEvent>(OnEffectRemoved);
    }

    private void OnEffectApplied(Entity<IPCFanStopStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!_gameTiming.ApplyingState)
        {
            if (TryComp<IPCThermalRegulationComponent>(args.Target, out var thermals))
            {
                thermals.FansOffOverride = true;
                Dirty(args.Target, thermals);
            }
        }
    }
    private void OnEffectRemoved(Entity<IPCFanStopStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (TryComp<IPCThermalRegulationComponent>(args.Target, out var thermals))
            {
                thermals.FansOffOverride = false;
                Dirty(args.Target, thermals);
            }
    }
}
