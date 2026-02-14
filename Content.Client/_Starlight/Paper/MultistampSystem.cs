using Content.Client.Items;
using Content.Shared._Starlight.Paper;
using Content.Shared.Paper;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Paper;

public sealed class MultistampSystem : SharedMultistampSystem
{
        [Dependency] private readonly SpriteSystem _sprite = default!;

        public override void Initialize()
        {
            base.Initialize();

            Subs.ItemStatus<MultistampComponent>(ent => new MultistampStatusControl(ent));
        }

        public override void SetMultistamp(EntityUid uid,
        MultistampComponent? stamps = null,
        StampComponent? stamp = null,
        bool playSound = false,
        EntityUid? user = null)
        {
            if (!Resolve(uid, ref stamps))
                return;

            base.SetMultistamp(uid, stamps, stamp, playSound, user);
            stamps.UiUpdateNeeded = true;

            if (!TryComp(uid, out SpriteComponent? sprite))
                return;
            if (stamps.Stamps.Count > stamps.CurrentEntry)
            {
                var current = stamps.Stamps[stamps.CurrentEntry];
                if (TryComp(current, out StampComponent? stampComp))
                    _sprite.LayerSetColor((uid, sprite), 1, stampComp.StampedColor);
            }
            else _sprite.LayerSetColor((uid, sprite), 1, Color.White);
        }
    }

