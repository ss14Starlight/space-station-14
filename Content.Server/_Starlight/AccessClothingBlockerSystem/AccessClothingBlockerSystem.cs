using System.Threading.Tasks;
using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Server.Audio;

namespace Content.Server._Starlight.AccessClothingBlockerSystem;

public sealed partial class AccessClothingBlockerSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private GibbingSystem _gibSystem = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessClothingBlockerComponent, GotEquippedEvent>(OnGotEquipped);
    }

    private async void OnGotEquipped(EntityUid uid, AccessClothingBlockerComponent component, GotEquippedEvent args)
    {
        var canUse = false;
        if (!TryComp<AccessReaderComponent>(uid, out var accessReader))
            canUse = true;

        if (component.Access != null)
        {
            var accesses = _accessReader.FindAccessTags(args.Equipee);
            if (accesses.Any(a => a.ToString() == component.Access))
                canUse = true;
        }

        else if (_accessReader.IsAllowed(args.Equipee, uid, accessReader) )
                canUse = true;

        if (canUse)
            return;

        EntityManager.EnsureComponent<UnremoveableComponent>(uid);
        await PopupWithDelays(uid, component);
        _gibSystem.Gib(args.Equipee, true);
        _explosionSystem.QueueExplosion(uid, "Default", 50, 5, 30, canCreateVacuum: false);
    }

    private async Task PopupWithDelays(EntityUid uid, AccessClothingBlockerComponent component)
    {
        var notifications = new[]
        {
            new { Message = Loc.GetString("access-clothing-blocker-notify-wrong-user-detected"), Delay = TimeSpan.FromSeconds(2), PopupType = PopupType.LargeCaution },
            new { Message = Loc.GetString("access-clothing-blocker-notify-inclusion-bolts"), Delay = TimeSpan.FromSeconds(2), PopupType = PopupType.LargeCaution },
            new { Message = Loc.GetString("access-clothing-blocker-notify-activate-self-destruction"), Delay = TimeSpan.FromSeconds(2), PopupType = PopupType.LargeCaution }
        };

        foreach (var notification in notifications)
        {

            _audioSystem.PlayPvs(component.BeepSound, uid);
            await PopupWithDelay(notification.Message, uid, notification.PopupType);
            await Task.Delay(notification.Delay);
        }

        for (int i = 10; i > 0; i--)
        {
            _audioSystem.PlayPvs(component.BeepSound, uid);
            await PopupWithDelay(i.ToString(), uid, PopupType.LargeCaution);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private Task PopupWithDelay(string message, EntityUid uid, PopupType popupType)
    {
        _popup.PopupEntity(message, uid, popupType);
        return Task.CompletedTask;
    }
}
