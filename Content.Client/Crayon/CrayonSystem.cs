using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

#region Starlight
using Content.Client._Starlight.Crayon.Overlays;
using Content.Client.Decals;
using Content.Shared.Decals;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
#endregion Starlight

namespace Content.Client.Crayon;

public sealed class CrayonSystem : SharedCrayonSystem
{
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    // Starlight-start
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly DecalPlacementSystem _placement = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    // Starlight-end
    
    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<CrayonComponent>(ent => new StatusControl(ent, _charges, _entityManager));

        // Starlight-start
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<CrayonComponent, HandDeselectedEvent>(OnHandDeselected);
        SubscribeLocalEvent<CrayonComponent, GotUnequippedHandEvent>(OnUnequip);
        SubscribeLocalEvent<CrayonComponent, ComponentShutdown>(OnComponentShutdown);
        // Starlight-end
    }

    private sealed class StatusControl : Control
    {
        private readonly Entity<CrayonComponent> _crayon;
        private readonly SharedChargesSystem _charges;
        private readonly RichTextLabel _label;
        private readonly int _capacity;

        public StatusControl(Entity<CrayonComponent> crayon, SharedChargesSystem charges, EntityManager entityManage)
        {
            _crayon = crayon;
            _charges = charges;
            _capacity = entityManage.GetComponent<LimitedChargesComponent>(_crayon.Owner).MaxCharges;
            _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            // TODO: This call needs fixingUpdateOverlay(_crayon.Owner, _crayon.Comp, _crayon.Comp.SelectedState, _crayon.Comp.Rotation, _crayon.Comp.Color, _crayon.Comp.PreviewEnabled, _crayon.Comp.PreviewVisible, _crayon.Comp.OpaqueGhost); // Starlight-edit

            _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("crayon-drawing-label",
                ("color",_crayon.Comp.Color),
                // Starlight-start
                ("rotation",_crayon.Comp.Rotation),
                ("previewEnabled",_crayon.Comp.PreviewEnabled),
                ("previewVisible",_crayon.Comp.PreviewVisible),
                // Starlight-end
                ("state",_crayon.Comp.SelectedState),
                ("charges", _charges.GetCurrentCharges(_crayon.Owner)),
                ("capacity", _capacity)));
        }
    }

    // Starlight-start
    private void UpdateOverlay(EntityUid uid, CrayonComponent component, ProtoId<DecalPrototype>? state, float rotation, Color color, bool preview, bool previewVisible, bool opaqueGhost)
    {
        if (_player.LocalEntity == null || _handsSystem.GetActiveItem(_player.LocalEntity.Value) != uid)
            return;
        _overlay.RemoveOverlay<CrayonDecalGhostOverlay>();
        if (!preview||!previewVisible)
            return;
        var decal = state is { } id ? _prototypeManager.Index(id) : null;
        if (opaqueGhost)
            color.A = 0.5f;
        _overlay.AddOverlay(new CrayonDecalGhostOverlay(_placement, _transform, _sprite, _interaction, decal, -rotation, color));
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        RemoveOverlay();
    }

    private void OnHandDeselected(EntityUid uid, CrayonComponent component, ref HandDeselectedEvent args)
    {
        RemoveOverlay();
    }

    private void OnUnequip(EntityUid uid, CrayonComponent component, ref GotUnequippedHandEvent args)
    {
        if(args.Unequipped==uid) RemoveOverlay();
    }

    private void OnComponentShutdown(EntityUid uid, CrayonComponent component, ComponentShutdown args)
    {
        RemoveOverlay();
    }

    private void RemoveOverlay()
    {
        _overlay.RemoveOverlay<CrayonDecalGhostOverlay>();
    }
    // Starlight-end
}
