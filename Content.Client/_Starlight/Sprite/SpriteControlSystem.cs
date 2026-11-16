using Content.Client._Starlight.Humanoid;
using Content.Client.Items.Systems;
using Content.Shared._Starlight.Administration.Components;
using Content.Shared._Starlight.Sprite;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Sprite;

public sealed partial class SpriteControlSystem : EntitySystem
{
    [Dependency] private ItemSystem _item = null!;
    [Dependency] private SpriteSystem _sprite = null!;
    [Dependency] private InventorySystem _inventory = null!;
    [Dependency] private SharedHandsSystem _hands = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SpriteLayerGroupsSystem _groups = null!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private ISharedAdminManager _admin = null!;

    private readonly HashSet<int> _clothingIndices = [];
    private readonly HashSet<int> _heldIndices = [];

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<SpriteControlComponent> ent, ref ComponentShutdown args)
    {
        var comp = ent.Comp;

        if (!TerminatingOrDeleted(ent)
            && comp.Backup is { } backup
            && TryComp<SpriteComponent>(ent, out var sprite)
            && TryComp<SpriteComponent>(backup, out var backupSprite))
        {
            if (comp.Replaced)
                RestoreSprite((ent, sprite), (backup, backupSprite));
            else
                CopySpriteWideProperties(backupSprite, (ent, sprite));
        }

        if (comp.Backup is { } toDelete) QueueDel(toDelete);

        comp.Backup = null;
        comp.Applied = null;
        comp.Replaced = false;
        comp.SourceLayers.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<SpriteControlComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            var config = GetConfig(comp);

            var rebuild = comp.Applied != config
                || (comp.SourceEntity is { } src && !comp.UsedLiveSource && src != uid &&
                    HasComp<SpriteComponent>(src))
                || (comp is { UsedLiveSource: true, MirrorInterval: not null } && _timing.RealTime >= comp.NextMirror);

            if (rebuild)
            {
                Apply((uid, comp, sprite), config);
                if (comp.MirrorInterval is { } mirror)
                    comp.NextMirror = _timing.RealTime + TimeSpan.FromSeconds(Math.Max(mirror, 0.01f));
            }

            if (comp.Replaced)
                HideForeignLayers((uid, comp, sprite));
        }
    }

    private static SpriteControlConfig GetConfig(SpriteControlComponent comp) => new(comp.SourceEntity,
        comp.SourceProto, comp.Sprite, comp.MirrorInterval, comp.KeepClothing, comp.KeepHeld, comp.HideOwnLayers,
        comp.Offset, comp.Rotation, comp.Color, comp.DrawDepth, comp.NoRotation);

    private void Apply(Entity<SpriteControlComponent, SpriteComponent> ent, SpriteControlConfig config)
    {
        var (uid, comp, sprite) = ent;
        comp.Applied = config;

        if (!EnsureBackup((uid, sprite), comp, out var backup))
            return;

        var replaced = TryReplaceSprite((uid, comp, sprite), backup.Comp);

        if (replaced)
        {
            comp.SourceLayers.Clear();
            foreach (var layer in sprite.LayerData)
                comp.SourceLayers.Add(layer);

            _groups.ForgetAttachmentLayers(uid);
            RerenderAttachments(uid);

            if (!comp.HideOwnLayers && TryComp<AppearanceComponent>(uid, out var appearance))
                _appearance.QueueUpdate(uid, appearance);
        }
        else
        {
            comp.UsedLiveSource = false;
            comp.SourceLayers.Clear();

            if (comp.Replaced)
                RestoreSprite((uid, sprite), backup);
            else
                CopySpriteWideProperties(backup.Comp, (uid, sprite));
        }

        comp.Replaced = replaced;
        ApplyOverrides((uid, comp, sprite));
    }

    private bool EnsureBackup(Entity<SpriteComponent> ent, SpriteControlComponent comp,
        out Entity<SpriteComponent> backup)
    {
        if (comp.Backup is { } existing && TryComp<SpriteComponent>(existing, out var existingSprite))
        {
            backup = (existing, existingSprite);
            return true;
        }

        var uid = Spawn(null, MapCoordinates.Nullspace);
        var backupSprite = EnsureComp<SpriteComponent>(uid);
        _sprite.CopySprite(ent.AsNullable(), (uid, backupSprite));

        _groups.RemoveAttachmentLayers(ent, (uid, backupSprite));

        comp.Backup = uid;
        backup = (uid, backupSprite);
        return true;
    }

    private bool TryReplaceSprite(Entity<SpriteControlComponent, SpriteComponent> ent, SpriteComponent baseline)
    {
        var (uid, comp, sprite) = ent;

        if (comp.SourceEntity is { } source && source != uid && TryComp<SpriteComponent>(source, out var sourceSprite))
        {
            CopyKeepingScale((source, sourceSprite), (uid, sprite));
            comp.UsedLiveSource = true;
            return true;
        }

        comp.UsedLiveSource = false;

        if (comp.SourceProto is { } protoId
            && _proto.TryIndex(protoId, out var proto)
            && proto.TryComp<SpriteComponent>(out var protoSprite, EntityManager.ComponentFactory))
        {
            CopyKeepingScale((EntityUid.Invalid, protoSprite), (uid, sprite));
            return true;
        }

        if (comp.Sprite is not { } specifier) return false;

        if (!IsSpecifierPermitted(ent))
        {
            comp.Sprite = null;
            Log.Warning($"Entity {ToPrettyString(ent):entity} not permitted to use sprite specifier as ghost.");
            return false;
        }

        CopySpriteWideProperties(baseline, (uid, sprite));

        while (sprite.LayerData.Count > 0)
            _sprite.RemoveLayer((uid, sprite), sprite.LayerData.Count - 1);

        _sprite.AddLayer((uid, sprite), specifier);
        return true;
    }

    private void ApplyOverrides(Entity<SpriteControlComponent, SpriteComponent> ent)
    {
        var (uid, comp, sprite) = ent;

        if (comp.Offset is { } offset)
            _sprite.SetOffset((uid, sprite), offset);
        if (comp.Rotation is { } rotation)
            _sprite.SetRotation((uid, sprite), rotation);
        if (comp.Color is { } color)
            _sprite.SetColor((uid, sprite), color);
        if (comp.DrawDepth is { } depth)
            _sprite.SetDrawDepth((uid, sprite), depth);
        if (comp.NoRotation is { } noRot)
            sprite.NoRotation = noRot;
    }

    private void CopySpriteWideProperties(SpriteComponent from, Entity<SpriteComponent> to)
    {
        _sprite.SetOffset(to.AsNullable(), from.Offset);
        _sprite.SetRotation(to.AsNullable(), from.Rotation);
        _sprite.SetColor(to.AsNullable(), from.Color);
        _sprite.SetDrawDepth(to.AsNullable(), from.DrawDepth);
        to.Comp.NoRotation = from.NoRotation;
    }

    private void RestoreSprite(Entity<SpriteComponent> ent, Entity<SpriteComponent> backup)
    {
        CopyKeepingScale(backup, ent);

        _groups.ForgetAttachmentLayers(ent);
        RerenderAttachments(ent);

        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            _humanoid.UpdateSprite((ent, humanoid, ent.Comp));

        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    private void CopyKeepingScale(Entity<SpriteComponent> from, Entity<SpriteComponent> to)
    {
        var scale = to.Comp.Scale;
        _sprite.CopySprite(from.AsNullable(), to.AsNullable());
        _sprite.SetScale(to.AsNullable(), scale);
    }

    private void RerenderAttachments(EntityUid uid)
    {
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.NextItem(out var item))
            _item.VisualsChanged(item);

        foreach (var held in _hands.EnumerateHeld(uid))
            _item.VisualsChanged(held);
    }

    private void HideForeignLayers(Entity<SpriteControlComponent, SpriteComponent> ent)
    {
        var (uid, comp, sprite) = ent;
        _groups.Collect(uid, sprite, _clothingIndices, _heldIndices);

        for (var i = 0; i < sprite.LayerData.Count; i++)
        {
            var layer = sprite.LayerData[i];
            if (!layer.Visible || comp.SourceLayers.Contains(layer))
                continue;

            var hide = _clothingIndices.Contains(i) ? !comp.KeepClothing
                : _heldIndices.Contains(i) ? !comp.KeepHeld
                : comp.HideOwnLayers;

            if (hide)
                _sprite.LayerSetVisible((uid, sprite), i, false);
        }
    }

    private bool IsSpecifierPermitted(EntityUid uid)
    {
        if (!HasComp<AdminGhostComponent>(uid)) return true;
        return TryComp<ActorComponent>(uid, out var actor) &&
            _admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Bypass);
    }
}
