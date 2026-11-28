using System.IO;
using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Commands;
using Content.Shared._Starlight.Sprite;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Sprite;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed partial class SpriteControlCommand : ToolshedCommand
{
    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private ISerializationManager _ser = null!;

    [CommandImplementation("setsource")]
    public EntityUid SetBySourceEntity([PipedArgument] EntityUid uid, EntityUid? sourceUid) =>
        Modify(uid, sprite => sprite.SourceEntity = sourceUid);

    // Technically speaking you can just use clone:humanoidappearance but whateverrrrrr
    [CommandImplementation("setplayer")]
    public EntityUid SetBySourcePlayer(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        ICommonSession? sourcePlayer)
    {
        if (sourcePlayer is null) return SetBySourceEntity(uid, null);
        if (sourcePlayer.AttachedEntity is not null) return SetBySourceEntity(uid, sourcePlayer.AttachedEntity.Value);
        CommandMarkup.Error(ctx, "Source player has no attached entity to copy from.");
        return uid;
    }

    [CommandImplementation("setproto")]
    public EntityUid SetBySourcePrototype([PipedArgument] EntityUid uid, EntProtoId? sourcePrototype) =>
        Modify(uid, sprite => sprite.SourceProto = sourcePrototype);

    [CommandImplementation("setspecifier")]
    public EntityUid SetBySpecifier(IInvocationContext ctx, [PipedArgument] EntityUid uid, string? yaml)
    {
        if (TryParseSpriteSpecifier(ctx, yaml, out var specifier))
            ApplySpecifier(ctx, uid, specifier, HasBypass(ctx));
        return uid;
    }

    [CommandImplementation("layerconf")]
    public EntityUid ConfigureLayerVisibility([PipedArgument] EntityUid uid, bool keepClothing = false,
        bool keepHeld = false, bool hideOwnLayers = true) =>
        Modify(uid, sprite =>
        {
            sprite.KeepClothing = keepClothing;
            sprite.KeepHeld = keepHeld;
            sprite.HideOwnLayers = hideOwnLayers;
        }, ensure: false);

    [CommandImplementation("transconf")]
    public EntityUid ConfigureTransformProperties([PipedArgument] EntityUid uid, Vector2? offset = null,
        Angle? rotation = null, int? drawDepth = null, bool? noRotation = null) =>
        Modify(uid, sprite =>
        {
            sprite.Offset = offset;
            sprite.Rotation = rotation;
            sprite.DrawDepth = drawDepth;
            sprite.NoRotation = noRotation;
        }, ensure: false);

    [CommandImplementation("reset")]
    public EntityUid Reset([PipedArgument] EntityUid uid)
    {
        RemComp<SpriteControlComponent>(uid);
        return uid;
    }

    [CommandImplementation("setsource")]
    public IEnumerable<EntityUid> SetBySourceEntity([PipedArgument] IEnumerable<EntityUid> uid, EntityUid sourceUid) =>
        uid.Select(x => SetBySourceEntity(x, sourceUid));

    [CommandImplementation("setplayer")]
    public IEnumerable<EntityUid> SetBySourcePlayer(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        ICommonSession sourcePlayer) => uid.Select(x => SetBySourcePlayer(ctx, x, sourcePlayer));

    [CommandImplementation("setproto")]
    public IEnumerable<EntityUid> SetBySourcePrototype([PipedArgument] IEnumerable<EntityUid> uid,
        EntProtoId sourcePrototype) => uid.Select(x => SetBySourcePrototype(x, sourcePrototype));

    [CommandImplementation("setspecifier")]
    public IEnumerable<EntityUid> SetBySpecifier(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids,
        string? yaml = null)
    {
        if (!TryParseSpriteSpecifier(ctx, yaml, out var specifier)) return uids;
        var doBypass = HasBypass(ctx);
        var targets = uids.ToList();
        foreach (var uid in targets)
            ApplySpecifier(ctx, uid, specifier, doBypass);
        return targets;
    }

    [CommandImplementation("layerconf")]
    public IEnumerable<EntityUid> ConfigureLayerVisibility([PipedArgument] IEnumerable<EntityUid> uid,
        bool keepClothing = false, bool keepHeld = false, bool hideOwnLayers = true) =>
        uid.Select(x => ConfigureLayerVisibility(x, keepClothing, keepHeld, hideOwnLayers));

    [CommandImplementation("transconf")]
    public IEnumerable<EntityUid> ConfigureTransformProperties([PipedArgument] IEnumerable<EntityUid> uid,
        Vector2? offset = null, Angle? rotation = null, int? drawDepth = null, bool? noRotation = null) =>
        uid.Select(x => ConfigureTransformProperties(x, offset, rotation, drawDepth, noRotation));

    [CommandImplementation("reset")]
    public IEnumerable<EntityUid> Reset([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(Reset);

    private EntityUid Modify(EntityUid uid, Action<SpriteControlComponent> modify, bool ensure = true)
    {
        SpriteControlComponent? sprite;
        if (ensure)
            sprite = EnsureComp<SpriteControlComponent>(uid);
        else if (!TryComp(uid, out sprite))
            return uid;

        modify(sprite);
        EntityManager.Dirty(uid, sprite);
        return uid;
    }

    /*
     * Bypass check for admins
     * The executing admin must have +Bypass if they wish to set a
     * ghost entity to a sprite specifier to ensure the integrity of existing ghost themes.
     */
    private bool HasBypass(IInvocationContext ctx) =>
        ctx.Session is not null && _admin.HasAdminFlag(ctx.Session, AdminFlags.Bypass);

    private void ApplySpecifier(IInvocationContext ctx, EntityUid uid, SpriteSpecifier? specifier, bool doBypass)
    {
        if (HasComp<GhostComponent>(uid) && !doBypass)
        {
            CommandMarkup.Error(ctx, "You cannot alter ghosts to use a sprite specifier without +Bypass.");
            return;
        }

        Modify(uid, sprite =>
        {
            sprite.BypassPermitted = doBypass && specifier is not null;
            sprite.Sprite = specifier;
        });
    }

    private bool TryParseSpriteSpecifier(IInvocationContext ctx, string? yaml, out SpriteSpecifier? specifier)
    {
        specifier = null;
        if (yaml is null) return true; // return true because null is valid, false is just if malformed.
        var document = DataNodeParser.ParseYamlStream(new StringReader(yaml)).First();
        var node = document.Root;
        var errors = _ser.ValidateNode<SpriteSpecifier>(node).GetErrors().ToList();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                CommandMarkup.Error(ctx, $"{error.Node.Start}: {error.ErrorReason}");
            CommandMarkup.Error(ctx, "Unable to parse sprite specifier.");
            return false;
        }

        specifier = _ser.Read<SpriteSpecifier>(node, notNullableOverride: true);
        return true;
    }
}
