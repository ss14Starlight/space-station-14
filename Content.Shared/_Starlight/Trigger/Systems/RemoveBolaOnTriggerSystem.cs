using Content.Shared._Starlight.Trigger.Components.Effects;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Tag;
using Content.Shared.Trigger;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Trigger.Systems;

public sealed partial class RemoveBolaOnTriggerSystem : XOnTriggerSystem<RemoveBolaOnTriggerComponent>
{
    [Dependency] private SharedEnsnareableSystem _ensnareable = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> _bolaTag = "Bola";

    protected override void OnTrigger(Entity<RemoveBolaOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (!TryComp<EnsnareableComponent>(target, out var ensnareable))
            return;

        foreach (var snare in ensnareable.Container.ContainedEntities)
        {
            if (!TryComp<EnsnaringComponent>(snare, out var ensnaring) || !_tag.HasTag(snare, _bolaTag))
                continue;

            _ensnareable.ForceFree(snare, ensnaring);
            args.Handled = true;
            return;
        }
    }
}
