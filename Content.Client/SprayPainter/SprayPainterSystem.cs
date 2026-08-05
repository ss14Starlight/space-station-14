using System.Linq;
using Content.Client._Starlight.SprayPainter;
using Content.Client.Decals;
using Content.Client.Hands.Systems;
using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._Starlight.SprayPainter;
using Content.Shared.Decals;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using Content.Shared.SprayPainter.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.SprayPainter;

/// <summary>
/// Client-side spray painter functions. Caches information for spray painter windows and updates the UI to reflect component state.
/// </summary>
public sealed partial class SprayPainterSystem : SharedSprayPainterSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    // Starlight begin
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private DecalPlacementSystem _placement = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private HandsSystem _hands = default!;
    // Starlight end

    public List<SprayPainterDecalEntry> Decals = [];
    public Dictionary<string, List<string>> PaintableGroupsByCategory = new();
    public Dictionary<string, Dictionary<string, EntProtoId>> PaintableStylesByGroup = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<SprayPainterComponent>(ent => new StatusControl(ent));
        SubscribeLocalEvent<SprayPainterComponent, AfterAutoHandleStateEvent>(OnStateUpdate);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        // Starlight begin
        SubscribeLocalEvent<SprayPainterComponent, SprayPainterUpdateDecalEvent>(OnDecalUpdated);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<SprayPainterComponent, HandDeselectedEvent>(OnHandDeselected);
        SubscribeLocalEvent<SprayPainterComponent, HandSelectedEvent>(OnHandSelected);
        SubscribeLocalEvent<SprayPainterComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<SprayPainterComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
        SubscribeLocalEvent<SprayPainterComponent, ComponentShutdown>(OnComponentShutdown);
        // Starlight end

        CachePrototypes();
    }

    private void OnStateUpdate(Entity<SprayPainterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUi(ent);
    }

    protected override void UpdateUi(Entity<SprayPainterComponent> ent)
    {
        if (_ui.TryGetOpenUi(ent.Owner, SprayPainterUiKey.Key, out var bui))
            bui.Update();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<PaintableGroupCategoryPrototype>() || !args.WasModified<PaintableGroupPrototype>() || !args.WasModified<DecalPrototype>())
            return;

        CachePrototypes();
    }

    private void CachePrototypes()
    {
        PaintableGroupsByCategory.Clear();
        PaintableStylesByGroup.Clear();
        foreach (var category in Proto.EnumeratePrototypes<PaintableGroupCategoryPrototype>().OrderBy(x => x.ID))
        {
            var groupList = new List<string>();
            foreach (var groupId in category.Groups)
            {
                if (!Proto.Resolve(groupId, out var group))
                    continue;

                groupList.Add(groupId);
                PaintableStylesByGroup[groupId] = group.Styles;
            }

            if (groupList.Count > 0)
                PaintableGroupsByCategory[category.ID] = groupList;
        }

        Decals.Clear();
        foreach (var decalPrototype in Proto.EnumeratePrototypes<DecalPrototype>().OrderBy(x => x.ID))
        {
            if (!decalPrototype.Tags.Contains("station")
                && !decalPrototype.Tags.Contains("markings")
                || decalPrototype.Tags.Contains("dirty"))
                continue;

            Decals.Add(new SprayPainterDecalEntry(decalPrototype.ID, decalPrototype.Sprite));
        }
    }

    private sealed class StatusControl : Control
    {
        private readonly RichTextLabel _label;
        private readonly Entity<SprayPainterComponent> _entity;
        private DecalPaintMode? _lastPaintingDecals = null;

        public StatusControl(Entity<SprayPainterComponent> ent)
        {
            _entity = ent;
            _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_entity.Comp.DecalMode == _lastPaintingDecals)
                return;

            _lastPaintingDecals = _entity.Comp.DecalMode;

            string modeLocString = _entity.Comp.DecalMode switch
            {
                DecalPaintMode.Add => "spray-painter-item-status-add",
                DecalPaintMode.Remove => "spray-painter-item-status-remove",
                _ => "spray-painter-item-status-off"
            };

            _label.SetMarkupPermissive(Robust.Shared.Localization.Loc.GetString("spray-painter-item-status-label",
                ("mode", Robust.Shared.Localization.Loc.GetString(modeLocString))));
        }
    }

    #region Starlight

    private void UpdateOverlay(SprayPainterComponent comp)
    {
        _overlay.RemoveOverlay<SprayPainterDecalGhostOverlay>();
        if (!comp.ShowDecalPreview) return;
        if (!_prototypeManager.HasIndex(comp.SelectedDecal)) return;
        var decal = _prototypeManager.Index(comp.SelectedDecal);
        var color = comp.SelectedDecalColor ?? Color.White;
        if (comp.OpaqueGhost) color.A /= 2;
        _overlay.AddOverlay(new SprayPainterDecalGhostOverlay(_placement, _transform, _sprite, _interaction, decal,
            comp.SelectedDecalAngle, color, comp.SnapDecals));
    }

    private void OnDecalUpdated(Entity<SprayPainterComponent> ent, ref SprayPainterUpdateDecalEvent args) =>
        UpdateOverlay(ent);

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (!TryComp<SprayPainterComponent>(_hands.GetActiveHandEntity(), out var comp)) return;
        UpdateOverlay(comp);
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent args) => RemoveOverlay();

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args) => RemoveOverlay();

    private void OnHandDeselected(Entity<SprayPainterComponent> ent, ref HandDeselectedEvent args) =>
        RemoveOverlay();

    private void OnHandSelected(Entity<SprayPainterComponent> ent, ref HandSelectedEvent args) =>
        UpdateOverlay(ent);

    private void OnGotEquippedHand(Entity<SprayPainterComponent> ent, ref GotEquippedHandEvent args)
    {
        if (_hands.GetActiveHandEntity() != ent) return;
        UpdateOverlay(ent);
    }

    private void OnGotUnequippedHand(Entity<SprayPainterComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (args.Unequipped != ent.Owner) return;
        RemoveOverlay();
    }

    private void OnComponentShutdown(Entity<SprayPainterComponent> ent, ref ComponentShutdown args) =>
        RemoveOverlay();

    private void RemoveOverlay() => _overlay.RemoveOverlay<SprayPainterDecalGhostOverlay>();

    #endregion

}

/// <summary>
/// A spray paintable decal, mapped by ID.
/// </summary>
public sealed record SprayPainterDecalEntry(string Name, SpriteSpecifier Sprite);
