using Content.Shared.Nutrition.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Tools.Systems;
using JetBrains.Annotations;

namespace Content.Shared.Nutrition.EntitySystems
{
    [UsedImplicitly]
    public abstract partial class SharedCreamPieSystem : EntitySystem
    {
        [Dependency] private SharedStunSystem _stunSystem = default!;
        [Dependency] private SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();

        SubscribeLocalEvent<CreamPieComponent, ThrowDoHitEvent>(OnCreamPieHit);
        SubscribeLocalEvent<CreamPieComponent, LandEvent>(OnCreamPieLand);
        SubscribeLocalEvent<CreamPiedComponent, ThrowHitByEvent>(OnCreamPiedHitBy);
        SubscribeLocalEvent<CreamPieComponent, BeforeToolRefinedEvent>(OnToolRefine);
        SubscribeLocalEvent<CreamPiedComponent, RejuvenateEvent>(OnRejuvenate);
    }

        public void SplatCreamPie(Entity<CreamPieComponent> creamPie)
        {
            // Already splatted! Do nothing.
            if (creamPie.Comp.Splatted)
                return;

            creamPie.Comp.Splatted = true;

            SplattedCreamPie(creamPie);
        }

        protected virtual void SplattedCreamPie(Entity<CreamPieComponent, EdibleComponent?> entity) { }

        public void SetCreamPied(EntityUid uid, CreamPiedComponent creamPied, bool value)
        {
            if (value == creamPied.CreamPied)
                return;

            creamPied.CreamPied = value;

            if (TryComp(uid, out AppearanceComponent? appearance))
            {
                _appearance.SetData(uid, CreamPiedVisuals.Creamed, value, appearance);
            }
        }

        private void OnCreamPieLand(Entity<CreamPieComponent> entity, ref LandEvent args)
        {
            SplatCreamPie(entity);
        }

        private void OnCreamPieHit(Entity<CreamPieComponent> entity, ref ThrowDoHitEvent args)
        {
            SplatCreamPie(entity);
        }

    private void OnCreamPiedHitBy(Entity<CreamPiedComponent> creamPied, ref ThrowHitByEvent args)
    {
        if (creamPied.Comp.CreamPied || !Exists(args.Thrown) || !TryComp<CreamPieComponent>(args.Thrown, out var creamPie))
            return;

        // TODO: Check if they even have a head that can be hit.
        SetCreamPied(creamPied.AsNullable(), true);
        _stunSystem.TryUpdateParalyzeDuration(creamPied.Owner, creamPie.ParalyzeTime);

        // Throwing is not predicted, so the thrower is not equal to the client predicting the collision, so we cannot pass in a user.
        // TODO: Make the popup API sane.
        if (_net.IsClient)
            return;

        // Shown only to the player that was hit.
        _popup.PopupEntity(
            Loc.GetString(
                "cream-pied-component-on-hit-by-message",
                ("thrown", args.Thrown)),
            creamPied.Owner, creamPied.Owner);

        var otherPlayers = Filter.PvsExcept(creamPied.Owner);

        // Show to everyone else.
        _popup.PopupEntity(
            Loc.GetString(
                "cream-pied-component-on-hit-by-message-others",
                ("owner", Identity.Entity(creamPied.Owner, EntityManager)),
                ("thrown", args.Thrown)),
            creamPied.Owner, otherPlayers, false);
    }

    private void OnRejuvenate(Entity<CreamPiedComponent> ent, ref RejuvenateEvent args)
    {
        SetCreamPied(ent.AsNullable(), false);
    }

    // TODO
    // A regression occured here. Previously creampies would activate their hidden payload if you tried to eat them.
    // However, the refactor to IngestionSystem caused the event to not be reached,
    // because eating is blocked if an item is inside the food.

    private void OnToolRefine(Entity<CreamPieComponent> ent, ref BeforeToolRefinedEvent args)
    {
        ActivatePayload(ent);
    }
}
