using Content.Server.Ghost.Roles.Components;
using Content.Shared._Starlight.Xenobiology.Potions;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server._Starlight.Xenobiology.Potions;

public sealed class SlimeSentiencePotionComponentSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _sharedMindSystem = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSentiencePotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeSentiencePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (_entityManager.TryGetComponent<MindContainerComponent>(args.Target.Value, out _)) return;
        _sharedMindSystem.MakeSentient(args.Target.Value);
        
        // Below is copied from MakeSentientEntityEffectSystem with some modifications
        if (TryComp(args.Target.Value, out GhostRoleComponent? ghostRole))
            return;

        ghostRole = AddComp<GhostRoleComponent>(args.Target.Value);
        EnsureComp<GhostTakeoverAvailableComponent>(args.Target.Value);

        ghostRole.RoleName = MetaData(args.Target.Value).EntityName;
        ghostRole.RoleDescription = Loc.GetString("ghost-role-information-slime-sentience-potion-description");
        
        PredictedQueueDel(args.Used);
    }
}