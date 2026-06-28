using Content.Shared.Trigger;
using Content.Shared.Pointing;

using Content.Shared._Starlight.Trigger.Components;

namespace Content.Server._Starlight.Trigger.Systems;

/// <summary>
/// Trigger interactions related to pointing.
/// </summary>
internal sealed class TriggerOnPointingSystem : TriggerOnXSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnPointedAtComponent, AfterGotPointedAtEvent>(OnPointedAt);
    }

    private void OnPointedAt(Entity<TriggerOnPointedAtComponent> ent, ref AfterGotPointedAtEvent args)
    {
        Trigger.Trigger(ent.Owner, args.Pointer, ent.Comp.KeyOut);
    }
}
