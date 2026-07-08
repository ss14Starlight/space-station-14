// Shared plushie system - handles eating prevention
using Content.Shared.Nutrition;

namespace Content.Shared._Starlight.Plushies;

/// <summary>
/// Shared system that prevents plushies with CuddleMessage from being eaten.
/// Must be in Shared project since IngestionSystem is shared.
/// </summary>
public sealed class SharedPlushieSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Prevent eating for cuddly plushies
        SubscribeLocalEvent<CuddleMessageComponent, IngestibleEvent>(OnIngestible);
    }

    /// <summary>
    /// Prevents plushies with cuddle messages from being eaten.
    /// Only blocks if PreventEating is true in the component.
    /// </summary>
    private void OnIngestible(Entity<CuddleMessageComponent> entity, ref IngestibleEvent args)
    {
        if (entity.Comp.PreventEating)
            args.Cancelled = true;
    }
}
