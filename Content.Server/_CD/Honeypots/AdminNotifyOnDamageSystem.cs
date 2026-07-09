using Content.Server.Chat.Managers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._CD.Honeypots;

public sealed partial class AdminNotifyOnDamageSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminNotifyOnDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<AdminNotifyOnDamageComponent> entity, ref DamageChangedEvent args)
    {
        var posFound = _transform.TryGetMapOrGridCoordinates(entity, out var gridPos);

        if (args.Origin != null)
            _chat.SendAdminAlert(args.Origin.Value, $"damaged honeypot: \"{ToPrettyString(entity)}\" at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not found]")}");
        else
            _chat.SendAdminAlert($"honeypot \"{ToPrettyString(entity)}\" got damaged at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not found]")}");
    }
}
