using Content.Server._Starlight.Honeypot.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Damage.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Honeypot;

/// <summary>
/// Notifies admins when an entity with <see cref="AdminNotifyOnDamageComponent"/> is damaged.
/// </summary>
public sealed partial class AdminNotifyOnDamageSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminNotifyOnDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<AdminNotifyOnDamageComponent> entity, ref DamageChangedEvent args)
    {
        var posFound = _transform.TryGetMapOrGridCoordinates(entity, out var gridPos);
        if (!args.DamageIncreased) return;
        if (_gameTiming.CurTime - entity.Comp.LastNotif < entity.Comp.NotifyCooldown) return;
        entity.Comp.LastNotif = _gameTiming.CurTime;

        // Send the actual alert message.
        if (args.Origin != null)
            _chat.SendAdminAlert(args.Origin.Value,
                $"damaged {entity.Comp.Subject}: \"{ToPrettyString(entity)}\" at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not found]")}");
        else
            _chat.SendAdminAlert(
                $"{entity.Comp.Subject} \"{ToPrettyString(entity)}\" got damaged at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not found]")}");

        // Follow up with a second line of only cmdlinks to the entity and aggressor.
        var links = $"[color=#ff0000]Click to [/color][cmdlink=\"Warp to {FormattedMessage.EscapeStringParameter(entity.Comp.Subject)}\" command=\"tpto {GetNetEntity(entity.Owner)}\"/]";
        if (args.Origin != null)
            links += $"[color=#ff0000] or [/color][cmdlink=\"Warp to attacker\" command=\"tpto {GetNetEntity(args.Origin.Value)}\"/]";
        _chat.SendAdminAlertNoFormatOrEscape(links);
    }

}
