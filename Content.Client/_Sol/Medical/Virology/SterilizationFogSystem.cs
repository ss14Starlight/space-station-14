using Content.Shared._Sol.Medical.Virology.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Sol.Medical.Virology;

/// <summary>
/// Fades sterilization fog sprites after the hold phase and before the exit door opens.
/// </summary>
public sealed class SterilizationFogSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SterilizationFogComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var fog, out var sprite))
        {
            var now = _timing.CurTime;
            if (now < fog.FadeStartsAt)
            {
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(0.85f));
                continue;
            }

            if (fog.FadeEndsAt <= fog.FadeStartsAt || now >= fog.FadeEndsAt)
            {
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(0f));
                continue;
            }

            var progress = (float)((now - fog.FadeStartsAt) / (fog.FadeEndsAt - fog.FadeStartsAt));
            var alpha = Math.Clamp(0.85f * (1f - progress), 0f, 0.85f);
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));
        }
    }
}
