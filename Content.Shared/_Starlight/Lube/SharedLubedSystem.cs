using Content.Shared.Lube;
using Content.Shared.NameModifier.EntitySystems;

namespace Content.Shared._Starlight.Lube;

public abstract partial class SharedLubedSystem : EntitySystem
{
    [Dependency] protected NameModifierSystem _nameMod = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LubedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LubedComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    /// <summary>
    /// Removes the lubed condition from the target.
    /// </summary>
    public void RemoveLubed(EntityUid uid)
    {
        if (RemComp<LubedComponent>(uid))
            _nameMod.RefreshNameModifiers(uid);
    }

    private void OnInit(EntityUid uid, LubedComponent component, ComponentInit args)
    {
        _nameMod.RefreshNameModifiers(uid);
    }

    private void OnRefreshNameModifiers(Entity<LubedComponent> entity, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier("lubed-name-prefix");
    }
}
