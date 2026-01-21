using Content.Shared._Starlight.Language.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeBluespaceRadioPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeBluespaceRadioPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeBluespaceRadioPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<LanguageSpeakerComponent>(args.Target.Value, out _)) return;
        var activeRadioComponent = _entityManager.EnsureComponent<ActiveRadioComponent>(args.Target.Value);
        var intrinsicRadioTransmitterComponent = _entityManager.EnsureComponent<IntrinsicRadioTransmitterComponent>(args.Target.Value);
        foreach (var channel in ent.Comp.Channels)
        {
            activeRadioComponent.Channels.Add(channel);
            intrinsicRadioTransmitterComponent.Channels.Add(channel);
        }
        intrinsicRadioTransmitterComponent.Channels = ent.Comp.Channels;
        _entityManager.AddComponent<IntrinsicRadioReceiverComponent>(args.Target.Value);
        _sharedPopupSystem.PopupPredicted($"{MetaData(args.Target.Value).EntityName} can now always use the radio.", args.Target.Value, args.Target.Value);
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}