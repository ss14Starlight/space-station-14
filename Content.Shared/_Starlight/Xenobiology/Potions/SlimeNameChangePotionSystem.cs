using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NameModifier.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeNameChangePotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeNameChangePotionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SlimeNameChangePotionComponent, SlimeNameChangePotionNewNameChangedMessage>(OnSlimePotionNameChanged);
    }
    
    private void OnAfterInteract(Entity<SlimeNameChangePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!_entityManager.TryGetComponent<MindContainerComponent>(args.Target.Value,
                out _)) return;
        var oldName = MetaData(args.Target.Value).EntityName;
        _metaDataSystem.SetEntityName(args.Target.Value, ent.Comp.AssignedName);
        if (args.User != args.Target.Value)
            _sharedPopupSystem.PopupPredicted($"{oldName} is now named {MetaData(args.Target.Value).EntityName}.", args.User, args.User);
        _sharedPopupSystem.PopupPredicted($"You are now named {MetaData(args.Target.Value).EntityName}.", args.Target.Value, args.Target.Value);
        PredictedQueueDel(args.Used);
    }

    private void OnSlimePotionNameChanged(EntityUid uid, SlimeNameChangePotionComponent slimeSentiencePotionComponent, SlimeNameChangePotionNewNameChangedMessage args)
    {
        slimeSentiencePotionComponent.AssignedName = args.NewName;
        Dirty(uid, slimeSentiencePotionComponent);
    }
}

[Serializable, NetSerializable]
public enum SlimeNameChangePotionUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SlimeNameChangePotionNewNameChangedMessage(string newName) : BoundUserInterfaceMessage
{
    public string NewName { get; } = newName;
}
