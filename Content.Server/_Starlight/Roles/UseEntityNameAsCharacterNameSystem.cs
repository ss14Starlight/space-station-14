using Content.Shared._Starlight.Roles.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Roles;

/// <inheritdoc cref="UseEntityNameAsCharacterNameComponent"/>
public sealed partial class UseEntityNameAsCharacterNameSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UseEntityNameAsCharacterNameComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(Entity<UseEntityNameAsCharacterNameComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        // The mind was named after the profile, but this mob has its own name.
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        mind.CharacterName = Name(ent);
        Dirty(mindId, mind);
    }
}
