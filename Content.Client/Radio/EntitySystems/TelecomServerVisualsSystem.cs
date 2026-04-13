using System.Linq;
using Content.Client.Radio.EntitySystems;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Radio.EntitySystems
{
    public sealed class TelecomServerVisualsSystem : EntitySystem
    {
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SpriteSystem _sprite = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private readonly HashSet<EntityUid> _overheatedEntities = new();
        private const int LogoLayer = 1;

        public override void Initialize()
        {
            base.Initialize();
            
            SubscribeLocalEvent<TelecomServerComponent, AppearanceChangeEvent>(OnAppearanceChange);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_overheatedEntities.Count == 0)
            {
                return;
            }

            var pulse = (MathF.Sin((float)_timing.CurTime.TotalSeconds * 6f) + 1f) * 0.5f;
            var color = Color.InterpolateBetween(Color.White, Color.Red, pulse);

            foreach (var uid in _overheatedEntities.ToArray())
            {
                if (!TryComp<SpriteComponent>(uid, out var sprite))
                {
                    _overheatedEntities.Remove(uid);
                    continue;
                }

                _sprite.LayerSetColor((uid, sprite), LogoLayer, color);
            }
        }

        private void OnAppearanceChange(EntityUid uid, TelecomServerComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
            {
                return;
            }

            if (!_appearance.TryGetData<bool>(uid, TelecomServerVisuals.Overheated, out var overheated, args.Component))
            {
                if (_overheatedEntities.Remove(uid))
                {
                    _sprite.LayerSetColor((uid, args.Sprite), LogoLayer, Color.White);
                }

                return;
            }

            if (overheated)
            {
                _overheatedEntities.Add(uid);
            }
            else
            {
                if (_overheatedEntities.Remove(uid))
                {
                    _sprite.LayerSetColor((uid, args.Sprite), LogoLayer, Color.White);
                }
            }
        }
    }
}
