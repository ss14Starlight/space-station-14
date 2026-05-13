using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Radio.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Radio.Systems;

/// <summary>
/// Handles sprite overrides for the encryption key.
/// </summary>
public sealed class ClientEncryptionKeySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly StarlightEntitySystem _sl = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EncryptionKeyComponent, AfterAutoHandleStateEvent>(OnAutoHandleState);
    }

    private void OnAutoHandleState(EntityUid uid, EncryptionKeyComponent component, AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)) return;
        if(component.CustomBaseState is null) RestoreLayer((uid, sprite), 0);
        else
        {
            var rsi = component.CustomBaseRsi is not null
                ? new RSI(component.ExpectedSpriteSize, new ResPath(component.CustomBaseRsi), sprite.AllLayers.Count())
                : null;
            _sprite.LayerSetRsi((uid, sprite), 0, rsi, component.CustomBaseState);
        }
        if(component.CustomIconState is null) RestoreLayer((uid, sprite), 1);
        else
        {
            var rsi = component.CustomIconRsi is not null
                ? new RSI(component.ExpectedSpriteSize, new ResPath(component.CustomIconRsi), sprite.AllLayers.Count())
                : null;
            _sprite.LayerSetRsi((uid, sprite), 1, rsi, component.CustomIconState);
        }

        Dirty(uid, component);
    }

    private void RestoreLayer(Entity<SpriteComponent> entity, int index)
    {
        var meta = MetaData(entity);
        if (meta.EntityPrototype is null) return;
        if (!_sl.TryGetSingleton(meta.EntityPrototype, out var singleton) || singleton == EntityUid.Invalid) return;
        if (!TryComp<SpriteComponent>(singleton, out var sprite)) return;
        if (!_sprite.TryGetLayer((singleton, sprite), index, out var layer, false)) return;
        _sprite.LayerSetRsi((entity.Owner, entity.Comp), index, layer.RSI, layer.State);
    }

    // Saved here commented out so that if/when the PR I made to RT gets merged, I can swap back to this instead.
    // private bool TryGetPrototypeSprite(EntityUid uid, [NotNullWhen(true)] out SpriteComponent? sprite)
    // {
    //     sprite = null;
    //     var protoId = MetaData(uid).EntityPrototype;
    //     if(protoId is null) return false;
    //     if (!_proto.Resolve(protoId, out var proto)) return false;
    //     if (!proto.Components.TryGetComponent(_factory.GetRegistration<SpriteComponent>().Name,
    //             out var iComp)) return false;
    //     if (iComp is not SpriteComponent protoSprite) return false;
    //     sprite = protoSprite;
    //     return true;
    // }
}
