using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Fluids;
using Content.Shared.IdentityManagement;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Tools.Systems;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Nutrition.EntitySystems;

public abstract partial class SharedCreamPieSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CreamPieComponent, ThrowDoHitEvent>(OnCreamPieHit);
        SubscribeLocalEvent<CreamPieComponent, LandEvent>(OnCreamPieLand);
        SubscribeLocalEvent<CreamPiedComponent, ThrowHitByEvent>(OnCreamPiedHitBy);
        SubscribeLocalEvent<CreamPieComponent, BeforeToolRefinedEvent>(OnToolRefine);
        SubscribeLocalEvent<CreamPiedComponent, RejuvenateEvent>(OnRejuvenate);
    }

    /// <summary>
    /// SPLAT!
    /// </summary>
    public void SplatCreamPie(Entity<CreamPieComponent> creamPie)
    {
        if (creamPie.Comp.Splatted)
            return;

        creamPie.Comp.Splatted = true;
        Dirty(creamPie);

        if (_net.IsServer)
        {
            var coordinates = Transform(creamPie).Coordinates;
            _audio.PlayPvs(creamPie.Comp.Sound, coordinates);
        }

        if (TryComp<EdibleComponent>(creamPie, out var edibleComp))
        {
            if (_solutions.TryGetSolution(creamPie.Owner, edibleComp.Solution, out _, out var solution))
                _puddle.TrySpillAt(creamPie.Owner, solution, out _, false);

            _ingestion.SpawnTrash((creamPie.Owner, edibleComp));
        }

        ActivatePayload(creamPie);
        PredictedQueueDel(creamPie);
    }

    /// <summary>
    /// Drop any item hidden in the cream pie and trigger it.
    /// </summary>
    public void ActivatePayload(EntityUid uid)
    {
        if (_net.IsClient)
            return;

        if (_itemSlots.TryGetSlot(uid, CreamPieComponent.PayloadSlotName, out var itemSlot)
            && _itemSlots.TryEject(uid, itemSlot, user: null, out var item)
            && TryComp<TimerTriggerComponent>(item.Value, out var timerTrigger))
            _trigger.ActivateTimerTrigger((item.Value, timerTrigger));
    }

    public void SetCreamPied(Entity<CreamPiedComponent?> ent, bool value)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (value == ent.Comp.CreamPied)
            return;

        ent.Comp.CreamPied = value;
        Dirty(ent);
        _appearance.SetData(ent.Owner, CreamPiedVisuals.Creamed, value);
    }

    /// <summary>
    /// Compatibility overload for fork systems which still pass the resolved component separately.
    /// </summary>
    public void SetCreamPied(EntityUid uid, CreamPiedComponent creamPied, bool value)
    {
        SetCreamPied((uid, creamPied), value);
    }

    private void OnCreamPieLand(Entity<CreamPieComponent> ent, ref LandEvent args)
    {
        SplatCreamPie(ent);
    }

    private void OnCreamPieHit(Entity<CreamPieComponent> ent, ref ThrowDoHitEvent args)
    {
        SplatCreamPie(ent);
    }

    private void OnCreamPiedHitBy(Entity<CreamPiedComponent> creamPied, ref ThrowHitByEvent args)
    {
        if (!Exists(args.Thrown) || !TryComp<CreamPieComponent>(args.Thrown, out var creamPie))
            return;

        _stunSystem.TryUpdateParalyzeDuration(creamPied.Owner, TimeSpan.FromSeconds(creamPie.ParalyzeTime));

        if (creamPied.Comp.CreamPied)
            return;

        SetCreamPied(creamPied.AsNullable(), true);

        if (_net.IsClient)
            return;

        _popup.PopupEntity(
            Loc.GetString(
                "cream-pied-component-on-hit-by-message",
                ("thrown", args.Thrown)),
            creamPied.Owner,
            creamPied.Owner);

        var otherPlayers = Filter.PvsExcept(creamPied.Owner);

        _popup.PopupEntity(
            Loc.GetString(
                "cream-pied-component-on-hit-by-message-others",
                ("owner", Identity.Entity(creamPied.Owner, EntityManager)),
                ("thrown", args.Thrown)),
            creamPied.Owner,
            otherPlayers,
            false);
    }

    private void OnRejuvenate(Entity<CreamPiedComponent> ent, ref RejuvenateEvent args)
    {
        SetCreamPied(ent.AsNullable(), false);
    }

    private void OnToolRefine(Entity<CreamPieComponent> ent, ref BeforeToolRefinedEvent args)
    {
        ActivatePayload(ent);
    }
}
