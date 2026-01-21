using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Computers.RemoteEye;
using Content.Shared._Starlight.Xenobiology.MiscItems;
using Content.Shared._Starlight.Xenobiology.Potions;
using Content.Shared.Actions;
using Content.Shared.Construction;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Xenobiology;

public sealed class XenobiologyConsoleSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;

    private static readonly EntProtoId _monkeyCubeName = "MonkeyCube";
    private static readonly EntProtoId _mutationPotionName = "SlimeMutationPotion";
    private static readonly EntProtoId _stabilizerPotionName = "SlimeStabilizerPotion";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobiologyConsoleComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<XenobiologyConsoleComponent, MachineDeconstructedEvent>(OnMachineDeconstruction);
        
        SubscribeLocalEvent<ConsoleGrabSlimeEvent>(OnConsoleGrabSlime);
        SubscribeLocalEvent<ConsolePlaceSlimeEvent>(OnConsolePlaceSlime);
        SubscribeLocalEvent<ConsolePlaceMonkeyEvent>(OnConsolePlaceMonkey);
        SubscribeLocalEvent<ConsoleRecycleMonkeyCorpseEvent>(OnConsoleRecycleMonkeyCorpse);
        SubscribeLocalEvent<ConsoleApplyMutationPotionEvent>(OnConsoleApplyMutationPotion);
        SubscribeLocalEvent<ConsoleApplyStabilizerPotionEvent>(OnConsoleApplyStabilizerPotion);
        SubscribeLocalEvent<ConsoleGetSlimeInfoEvent>(OnConsoleGetSlimeInfo);
    }

    private void OnAfterInteractUsing(Entity<XenobiologyConsoleComponent> entity, ref AfterInteractUsingEvent args)
    {
        if (_tagSystem.HasTag(args.Used, entity.Comp.MonkeyCubeTag))
        {
            entity.Comp.MonkeyCubes += 1;
            PredictedQueueDel(args.Used);
            RaiseLocalEvent(new ConsolePopupEvent(entity.Owner, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-monkey-cube-inserted", ("cubes", entity.Comp.MonkeyCubes)))));
            args.Handled = true;
            return;
        }

        if (_entityManager.HasComponent<SlimeMutationPotionComponent>(args.Used))
        {
            entity.Comp.MutationPotions += 1;
            PredictedQueueDel(args.Used);
            RaiseLocalEvent(new ConsolePopupEvent(entity.Owner, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-mutation-potion-inserted", ("potions", entity.Comp.MutationPotions)))));
            args.Handled = true;
            return;
        }

        if (_entityManager.HasComponent<SlimeStabilizerPotionComponent>(args.Used))
        {
            entity.Comp.StabilizerPotions += 1;
            PredictedQueueDel(args.Used);
            RaiseLocalEvent(new ConsolePopupEvent(entity.Owner, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-stabilizer-potion-inserted", ("potions", entity.Comp.StabilizerPotions)))));
            args.Handled = true;
            return;
        }
    }

    private void OnMachineDeconstruction(Entity<XenobiologyConsoleComponent> entity, ref MachineDeconstructedEvent args)
    {
        for (var i = 0; i < entity.Comp.MonkeyCubes; i++)
            SpawnNextToOrDrop(_monkeyCubeName, entity.Owner);
        for (var i = 0; i < entity.Comp.MutationPotions; i++)
            SpawnNextToOrDrop(_mutationPotionName, entity.Owner);
        for (var i = 0; i < entity.Comp.StabilizerPotions; i++)
            SpawnNextToOrDrop(_stabilizerPotionName, entity.Owner);
    }
    
    private bool VerifyComponents(InstantActionEvent args, [NotNullWhen(true)] out RemoteEyeActorComponent? remoteEyeActorComponent,
        [NotNullWhen(true)] out XenobiologyConsoleComponent? xenobiologyConsoleComponent, [NotNullWhen(true)] out EntityUid? remoteEntity)
    {
        remoteEyeActorComponent = null;
        xenobiologyConsoleComponent = null;
        remoteEntity = null;
        if (!_entityManager.TryGetComponent<RemoteEyeActorComponent>(args.Performer, out remoteEyeActorComponent))
        {
            return false;
        }
        if (remoteEyeActorComponent.VirtualItem == null)
        {
            return false;
        }
        if (!_entityManager.TryGetComponent<XenobiologyConsoleComponent>(remoteEyeActorComponent.VirtualItem, out xenobiologyConsoleComponent)) return false;
        if (remoteEyeActorComponent.RemoteEntity == null) return false;
        remoteEntity = remoteEyeActorComponent.RemoteEntity;
        return true;
    }

    private void OnConsoleGrabSlime(ConsoleGrabSlimeEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        xenobiologyConsoleComponent.SlimeContainer = _container.EnsureContainer<ContainerSlot>(remoteEntity.Value, XenobiologyConsoleComponent.SlimeContainerName);
        var slimeFound = false;
        foreach (var slime in _entityLookupSystem.GetEntitiesInRange<SlimeComponent>(Transform(remoteEntity.Value).Coordinates, 0.5F))
        {
            slimeFound = true;
            if (_container.Insert(slime.Owner, xenobiologyConsoleComponent.SlimeContainer))
                RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-slime-picked-up", ("name", MetaData(slime).EntityName)))));
            else
                RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-slime-picked-up-fail-full", ("name", MetaData(slime).EntityName)))));
            break;
        }
        if (!slimeFound)
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-slime-picked-up-fail-none-found"))));
        args.Handled = true;
    }

    private void OnConsolePlaceSlime(ConsolePlaceSlimeEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        xenobiologyConsoleComponent.SlimeContainer = _container.EnsureContainer<ContainerSlot>(remoteEntity.Value, XenobiologyConsoleComponent.SlimeContainerName);
        if (xenobiologyConsoleComponent.SlimeContainer.ContainedEntities.Count <= 0)
        {
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-slime-placed-down-fail-none-stored"))));
            args.Handled = true;
            return;
        }
        var slime = xenobiologyConsoleComponent.SlimeContainer.ContainedEntities[0];
        _container.Remove(slime, xenobiologyConsoleComponent.SlimeContainer, destination: Transform(remoteEntity.Value).Coordinates);
        RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-slime-placed-down", ("name", MetaData(slime).EntityName)))));
        args.Handled = true;
    }

    private void OnConsolePlaceMonkey(ConsolePlaceMonkeyEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        if (xenobiologyConsoleComponent.MonkeyCubes < 1)
        {
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-monkey-placed-fail-empty", ("cubes", xenobiologyConsoleComponent.MonkeyCubes)))));
            args.Handled = true;
            return;
        }
        _entityManager.PredictedSpawnAtPosition(xenobiologyConsoleComponent.MonkeyProtoId, Transform(remoteEntity.Value).Coordinates);
        xenobiologyConsoleComponent.MonkeyCubes -= 1;
        RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-monkey-placed", ("cubes", xenobiologyConsoleComponent.MonkeyCubes)))));
        args.Handled = true;
    }

    private void OnConsoleRecycleMonkeyCorpse(ConsoleRecycleMonkeyCorpseEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        var monkeysFound = 0;
        foreach (var possibleMonkey in _entityLookupSystem.GetEntitiesInRange(remoteEntity.Value, 0.5F))
        {
            if (_tagSystem.HasTag(possibleMonkey, xenobiologyConsoleComponent.MonkeyTag))
            {
                if (!_entityManager.TryGetComponent<DamageableComponent>(possibleMonkey, out var damageableComponent))
                    continue;
                if (damageableComponent.TotalDamage < 100) continue; // Slimes don't eat monkeys above 100 damage
                PredictedQueueDel(possibleMonkey);
                xenobiologyConsoleComponent.MonkeyCubes += 0.5D;
                monkeysFound += 1;
            }
        }

        RaiseLocalEvent(monkeysFound > 0
            ? new ConsolePopupEvent(remoteEntity.Value,
                FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-monkey-recycled", ("monkeys", monkeysFound), ("cubes", xenobiologyConsoleComponent.MonkeyCubes))))
            : new ConsolePopupEvent(remoteEntity.Value,
                FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-monkey-recycled-failed-none"))));

        args.Handled = true;
    }

    private void OnConsoleApplyMutationPotion(ConsoleApplyMutationPotionEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        if (xenobiologyConsoleComponent.MutationPotions < 1)
        {
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-mutation-potion-applied-failed-empty"))));
            args.Handled = true;
            return;
        }
        foreach (var slime in _entityLookupSystem.GetEntitiesInRange<SlimeComponent>(Transform(remoteEntity.Value).Coordinates, 0.5F))
        {
            slime.Comp.MutationChance += SlimeMutationPotionComponent.MutationChangeAmount;
            xenobiologyConsoleComponent.MutationPotions -= 1;
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-mutation-potion-applied", ("name", MetaData(slime).EntityName), ("chance", slime.Comp.MutationChance)))));
            break;
        }
        args.Handled = true;
    }
    
    private void OnConsoleApplyStabilizerPotion(ConsoleApplyStabilizerPotionEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        if (xenobiologyConsoleComponent.StabilizerPotions < 1)
        {
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-stabilizer-potion-applied-failed-empty"))));
            args.Handled = true;
            return;
        }
        foreach (var slime in _entityLookupSystem.GetEntitiesInRange<SlimeComponent>(Transform(remoteEntity.Value).Coordinates, 0.5F))
        {
            slime.Comp.MutationChance += SlimeStabilizerPotionComponent.MutationChangeAmount;
            xenobiologyConsoleComponent.StabilizerPotions -= 1;
            RaiseLocalEvent(new ConsolePopupEvent(remoteEntity.Value, FormattedMessage.FromMarkupPermissive(Loc.GetString("xenobiology-console-stabilizer-potion-applied", ("name", MetaData(slime).EntityName), ("chance", slime.Comp.MutationChance)))));
            break;
        }
        args.Handled = true;
    }

    private void OnConsoleGetSlimeInfo(ConsoleGetSlimeInfoEvent args)
    {
        if (!VerifyComponents(args, out var remoteEyeActorComponent, out var xenobiologyConsoleComponent, out var remoteEntity)) return;
        if (!remoteEyeActorComponent.VirtualItem.HasValue) return;
        foreach (var slime in _entityLookupSystem.GetEntitiesInRange<SlimeComponent>(Transform(remoteEntity.Value).Coordinates, 0.5F))
        {
            var ev = new ConsoleMsgToScannerEvent(args.Performer, slime.Owner);
            RaiseLocalEvent(remoteEyeActorComponent.VirtualItem.Value, ev);
            break;
        }
        args.Handled = true;
    }
}

public sealed partial class ConsoleGetSlimeInfoEvent : InstantActionEvent
{

}

public sealed partial class ConsoleGrabSlimeEvent : InstantActionEvent
{

}

public sealed partial class ConsolePlaceSlimeEvent : InstantActionEvent
{

}

public sealed partial class ConsolePlaceMonkeyEvent : InstantActionEvent
{

}

public sealed partial class ConsoleRecycleMonkeyCorpseEvent : InstantActionEvent
{

}

public sealed partial class ConsoleApplyMutationPotionEvent : InstantActionEvent
{

}

public sealed partial class ConsoleApplyStabilizerPotionEvent : InstantActionEvent
{

}

public sealed partial class ConsoleTextMsgEvent(EntityUid user, FormattedMessage message)
{
    public EntityUid User = user;
    public FormattedMessage Message = message;
}

public sealed partial class ConsolePopupEvent(EntityUid console, FormattedMessage message)
{
    public EntityUid Console = console;
    public FormattedMessage Message = message;
}