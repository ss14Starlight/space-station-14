using Content.Shared._Inferus.Standing;
using Content.Shared.Standing;
using Robust.Client.GameObjects;

namespace Content.Client._Inferus.Standing;

/// <summary>
/// Updates draw depth while crawling under furniture
/// </summary>
public sealed class UnderTableCrawlingSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<UnderTableCrawlingComponent, StandingStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var underTable, out var standing, out var sprite))
        {
            // Don't fight external draw-depth changes
            if (sprite.DrawDepth != underTable.NormalDrawDepth
                && sprite.DrawDepth != underTable.CrawlingUnderDrawDepth)
                continue;

            var depth = (!standing.Standing && underTable.IsCrawlingUnder)
                ? underTable.CrawlingUnderDrawDepth
                : underTable.NormalDrawDepth;

            _sprites.SetDrawDepth((uid, sprite), depth);
        }
    }
}
