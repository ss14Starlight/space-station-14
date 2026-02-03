using Content.Shared._Starlight.Antags.Vampires.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Antags.Vampires;
/// <summary>
/// Handles copying visual data from the vampire to its decoy.
/// </summary>
public sealed class VampireDecoySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VampireDecoyAppearanceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VampireDecoyAppearanceComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<VampireDecoyAppearanceComponent> ent, ref ComponentStartup args)
        => TryCopySprite(ent);

    private void OnAfterState(Entity<VampireDecoyAppearanceComponent> ent, ref AfterAutoHandleStateEvent args)
        => TryCopySprite(ent);

    private void TryCopySprite(Entity<VampireDecoyAppearanceComponent> ent)
    {
        if (!ent.Comp.Source.HasValue 
            || !TryComp<SpriteComponent>(ent.Owner, out var decoySprite) 
            || !TryComp<SpriteComponent>(ent.Comp.Source.Value, out var sourceSprite))
            return;

        _sprite.CopySprite((ent.Comp.Source.Value, sourceSprite), (ent.Owner, decoySprite));
    }
}
