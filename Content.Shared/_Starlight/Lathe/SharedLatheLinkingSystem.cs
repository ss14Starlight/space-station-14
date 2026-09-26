using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Materials;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Lathe;

/// <summary>
/// This handles...
/// </summary>
public sealed partial class SharedLatheLinkingSystem : EntitySystem
{
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheLinkingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LatheLinkingComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<LatheLinkingComponent, LinkAttemptEvent>(OnLinkingAttempt);
        SubscribeLocalEvent<LatheLinkingComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeAllEvent<LatheLinkingToggleEvent>(OnLinkingToggle);

    }

    private void OnLinkingToggle(LatheLinkingToggleEvent msg, EntitySessionEventArgs args)
    {
        if(args.SenderSession.AttachedEntity is not {} player) return;

        var uid = GetEntity(msg.entity);

        if (!TryComp<LatheLinkingComponent>(uid, out var comp)) return;

        if (!Exists(uid)) return;

        comp.Ejecting = msg.Ejecting;
        Dirty<LatheLinkingComponent>(uid!);
    }

    private void OnMapInit(Entity<LatheLinkingComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source)) return;

        var linkedEntities = _deviceLink.GetLinkedSinks((ent.Owner, source), ent.Comp.SourcePort);

        foreach (var sink in linkedEntities)
        {
            if (!TryComp<MaterialStorageComponent>(sink, out _)) continue;

            ent.Comp.LinkedEntity = sink;
            Dirty(ent);
        }
    }

    private void OnPortDisconnected(Entity<LatheLinkingComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.SourcePort || ent.Comp.LinkedEntity == null) return;
        ent.Comp.LinkedEntity = null;
        Dirty(ent);
    }

    private void OnLinkingAttempt(Entity<LatheLinkingComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.LinkedEntity != null) args.Cancel();

    }

    private void OnNewLink(Entity<LatheLinkingComponent> ent, ref NewLinkEvent args)
    {
        if (args.SinkPort != ent.Comp.SinkPort || !HasComp<MaterialStorageComponent>(args.Source)) return;

        ent.Comp.LinkedEntity = args.Sink;
        Dirty(ent);
    }
}

[Serializable, NetSerializable]
public sealed class LatheLinkingToggleEvent : EntityEventArgs
{
    public readonly NetEntity entity;
    public readonly bool Ejecting;

    public LatheLinkingToggleEvent(NetEntity entity, bool ejecting)
    {
        this.entity = entity;
        Ejecting = ejecting;
    }
}
