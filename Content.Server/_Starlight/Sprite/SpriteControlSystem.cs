using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Sprite;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Sprite;

public sealed partial class SpriteControlSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = null!;

    public override void Initialize()
    {
        base.Initialize();

        _admin.OnPermsChanged += OnPermsChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _admin.OnPermsChanged -= OnPermsChanged;
    }

    [SubscribeLocalEvent]
    private void OnStartup(Entity<SpriteControlComponent> ent, ref ComponentStartup args) => Validate(ent);

    [SubscribeLocalEvent]
    private void OnPlayerAttached(Entity<SpriteControlComponent> ent, ref PlayerAttachedEvent args) => Validate(ent);

    [SubscribeLocalEvent]
    private void OnGhostInit(Entity<GhostComponent> ent, ref ComponentInit args) => TryValidate(ent);

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player.AttachedEntity is { } uid)
            TryValidate(uid);
    }

    private void TryValidate(EntityUid uid)
    {
        if (TryComp<SpriteControlComponent>(uid, out var comp))
            Validate((uid, comp));
    }

    private void Validate(Entity<SpriteControlComponent> ent)
    {
        if (ent.Comp.Sprite == null || !HasComp<GhostComponent>(ent) || ent.Comp.BypassPermitted)
            return;

        ent.Comp.Sprite = null;
        Dirty(ent);
        Log.Warning($"Entity {ToPrettyString(ent):entity} not permitted to use sprite specifier as ghost.");
    }
}
