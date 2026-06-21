using Content.Shared._Funkystation.Footprints;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Funkystation.Footprints;

public sealed class FootprintSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FootprintComponent, ComponentStartup>(OnStartup);
        SubscribeNetworkEvent<FootprintStateEvent>(OnStateUpdated);
    }

    private void OnStartup(EntityUid uid, FootprintComponent component, ref ComponentStartup args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnStateUpdated(FootprintStateEvent args)
    {
        if (TryGetEntity(args.NetEntity, out var uid) && TryComp<FootprintComponent>(uid, out var comp))
        {
            UpdateVisuals(uid.Value, comp);
        }
    }

    private void UpdateVisuals(EntityUid uid, FootprintComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var rsiPath = new ResPath("/Textures/_Funkystation/Effects/footprints.rsi");

        for (var i = 0; i < component.Prints.Count; i++)
        {
            var print = component.Prints[i];

            if (!sprite.LayerExists(i))
                sprite.AddBlankLayer(i);

            sprite.LayerSetOffset(i, print.Offset);
            sprite.LayerSetRotation(i, print.Rotation);
            sprite.LayerSetColor(i, print.Color);
            sprite.LayerSetSprite(i, new SpriteSpecifier.Rsi(rsiPath, print.State));
        }
    }
}
