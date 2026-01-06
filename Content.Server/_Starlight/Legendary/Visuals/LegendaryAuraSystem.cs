using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Legendary.Visuals;

public sealed class LegendaryAuraSystem : EntitySystem
{
    private static readonly EntProtoId _auraPrototype = "LegendaryItemAuraEffect";

    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LegendaryAuraComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LegendaryAuraComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<LegendaryAuraComponent, GotEquippedHandEvent>(OnGotEquippedHand);

        SubscribeLocalEvent<LegendaryAuraComponent, EntGotInsertedIntoContainerMessage>(OnGotInsertedIntoContainer);
        SubscribeLocalEvent<LegendaryAuraComponent, EntGotRemovedFromContainerMessage>(OnGotRemovedFromContainer);
    }

    private void OnStartup(Entity<LegendaryAuraComponent> ent, ref ComponentStartup args) => UpdateAura(ent);

    private void OnShutdown(Entity<LegendaryAuraComponent> ent, ref ComponentShutdown args) => RemoveAura(ent);

    private void OnGotEquippedHand(Entity<LegendaryAuraComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.PickedUpOnce)
            return;

        ent.Comp.PickedUpOnce = true;
        RemoveAura(ent);
    }

    private void OnGotInsertedIntoContainer(Entity<LegendaryAuraComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        // If leg. entity being inserted into a hand container, treat it as "picked up"
        if (!ent.Comp.PickedUpOnce &&
            TryComp<HandsComponent>(args.Container.Owner, out var hands) &&
            _hands.TryGetHand((args.Container.Owner, hands), args.Container.ID, out _))
        {
            ent.Comp.PickedUpOnce = true;
        }

        RemoveAura(ent);
    }

    private void OnGotRemovedFromContainer(Entity<LegendaryAuraComponent> ent, ref EntGotRemovedFromContainerMessage args) => UpdateAura(ent);

    private void UpdateAura(Entity<LegendaryAuraComponent> ent)
    {
        if (ent.Comp.PickedUpOnce)
        {
            RemoveAura(ent);
            return;
        }

        if (_containers.IsEntityInContainer(ent.Owner))
        {
            RemoveAura(ent);
            return;
        }

        EnsureAura(ent);
    }

    private void EnsureAura(Entity<LegendaryAuraComponent> ent)
    {
        if (ent.Comp.AuraEntity is { } existing && Exists(existing))
            return;

        if (!_proto.HasIndex(_auraPrototype))
            return;

        var aura = Spawn(_auraPrototype, Transform(ent.Owner).Coordinates);
        _xform.SetParent(aura, ent.Owner);
        ent.Comp.AuraEntity = aura;
    }

    private void RemoveAura(Entity<LegendaryAuraComponent> ent)
    {
        if (ent.Comp.AuraEntity is not { } aura)
            return;

        if (Exists(aura))
            QueueDel(aura);

        ent.Comp.AuraEntity = null;
    }
}
