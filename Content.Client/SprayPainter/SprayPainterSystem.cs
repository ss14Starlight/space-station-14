using System.Linq;
using Content.Client.Decals;
using Content.Client.Hands.Systems;
using Content.Client.Items;
using Content.Client.Message;
using Content.Client.SprayPainter.Overlays;
using Content.Client.Stylesheets;
using Content.Shared.Decals;
using Content.Shared.Hands.Components;
using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using Content.Shared.SprayPainter.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.SprayPainter;

/// <summary>
/// Client-side spray painter functions. Caches information for spray painter windows and updates the UI to reflect component state.
/// </summary>
public sealed class SprayPainterSystem : SharedSprayPainterSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly DecalPlacementSystem _decalPlacement = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public List<SprayPainterDecalEntry> Decals = [];
    public Dictionary<string, List<string>> PaintableGroupsByCategory = new();
    public Dictionary<string, Dictionary<string, EntProtoId>> PaintableStylesByGroup = new();

    private readonly Dictionary<EntityUid, SprayPainterDecalGhostOverlay> _overlays = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<SprayPainterComponent>(ent => new StatusControl(ent));
        SubscribeLocalEvent<SprayPainterComponent, AfterAutoHandleStateEvent>(OnStateUpdate);
        SubscribeLocalEvent<SprayPainterComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CachePrototypes();
    }

    public override void Update(float frameTime) // Update is used to check if the spray painter is in the player's active hand and update the decal ghost overlay accordingly, since the overlay should only be visible when the spray painter is in hand, and there isn't a specific event for when an item is moved to or from the player's hand
    {
        base.Update(frameTime);

        // Update all active overlays to check if they should still exist
        var query = EntityQueryEnumerator<SprayPainterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateDecalGhostOverlay((uid, comp));
        }
    }

    private void OnStateUpdate(Entity<SprayPainterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUi(ent);
        UpdateDecalGhostOverlay(ent); // Update the decal ghost overlay whenever the component state updates, since changes to the decal mode or selected decal should be reflected in the overlay immediately
    }

    private void OnComponentShutdown(Entity<SprayPainterComponent> ent, ref ComponentShutdown args) // Clean up the decal ghost overlay when the spray painter component is removed, such as when the item is deleted or the component is removed for any reason
    {
        // Clean up overlay when the spray painter component is removed
        if (_overlays.Remove(ent.Owner, out var overlay))
        {
            _overlayManager.RemoveOverlay(overlay);
        }
    }

    private void UpdateDecalGhostOverlay(Entity<SprayPainterComponent> ent) // Check if the spray painter should have the decal ghost overlay, and add or remove it accordingly
    {
        var hasOverlay = _overlays.ContainsKey(ent.Owner);
        
        // Determine if we should have the overlay:
        // 1. Must be in Add mode
        // 2. Must be in the player's active hand
        var shouldHaveOverlay = ent.Comp.DecalMode == DecalPaintMode.Add 
            && IsSprayPainterInActiveHand(ent.Owner);

        if (shouldHaveOverlay && !hasOverlay)
        {
            // Create and add the overlay
            var overlay = new SprayPainterDecalGhostOverlay(_decalPlacement, _transform, _spriteSystem, ent.Owner);
            _overlays[ent.Owner] = overlay;
            _overlayManager.AddOverlay(overlay);
        }
        else if (!shouldHaveOverlay && hasOverlay)
        {
            // Remove the overlay
            if (_overlays.Remove(ent.Owner, out var overlay))
            {
                _overlayManager.RemoveOverlay(overlay);
            }
        }
    }

    private bool IsSprayPainterInActiveHand(EntityUid sprayPainterUid) // Check if the spray painter is in the player's active hand, which determines whether the decal ghost overlay should be shown
    {
        var player = _playerManager.LocalPlayer?.ControlledEntity;
        if (player is null)
            return false;

        if (!EntityManager.TryGetComponent(player, out HandsComponent? handsComp))
            return false;

        var activeHand = _hands.GetActiveItem((player.Value, handsComp));
        return activeHand == sprayPainterUid;
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

    public override void Shutdown() // Clean up all active overlays when the system is shutting down, such as when the player disconnects or the game is closing
    {
        base.Shutdown();

        // Clean up all active overlays
        foreach (var overlay in _overlays.Values)
        {
            _overlayManager.RemoveOverlay(overlay);
        }

        _overlays.Clear();
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
}

/// <summary>
/// A spray paintable decal, mapped by ID.
/// </summary>
public sealed record SprayPainterDecalEntry(string Name, SpriteSpecifier Sprite);
