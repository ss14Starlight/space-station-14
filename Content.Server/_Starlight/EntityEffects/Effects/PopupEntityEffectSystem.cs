using Content.Server.Popups;
using Content.Shared._Starlight.EntityEffects.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Popups;

namespace Content.Server._Starlight.EntityEffects.Effects;

public sealed class PopupEntityEffectSystem : EntityEffectSystem<TransformComponent, Popup>
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<Popup> args) => _popupSystem.PopupEntity(args.Effect.Text, entity.Owner);
}
