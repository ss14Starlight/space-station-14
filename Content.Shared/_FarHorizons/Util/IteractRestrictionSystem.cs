using Content.Shared._FarHorizons.Util.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Tag;

namespace Content.Shared._FarHorizons.Util;

public sealed class InteractRestrictionSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InteractRestrictionComponent, CheckItemCanBeUsedEvent>(CheckItemCanBeUsed);
    }

    private void CheckItemCanBeUsed(Entity<InteractRestrictionComponent> ent, ref CheckItemCanBeUsedEvent ev)
    {
        if (ev.Cancelled)
            return;

        var target = ev.Target;
        var user = ev.User;

        if (target != null && ent.Comp.RestrictInteractionTarget is InteractRestrictionList targetRestrict) {
            if (targetRestrict.Blacklist != null &&
                _tagSystem.HasAnyTag(target.Value, targetRestrict.Blacklist))
                    ev.Cancel();
            
            if (targetRestrict.Whitelist != null &&
                !_tagSystem.HasAnyTag(target.Value, targetRestrict.Whitelist))
                    ev.Cancel();
            
            if (ev.Cancelled)
                _popupSystem.PopupClient(Loc.GetString("interact-restriction-restricted-target", ("item", Identity.Entity(ent, EntityManager)), ("target", Identity.Entity(target.Value, EntityManager))), user);
        }

        if (ent.Comp.RestrictInteractionSource is InteractRestrictionList sourceRestrict) {
            if (sourceRestrict.Blacklist != null &&
                _tagSystem.HasAnyTag(user, sourceRestrict.Blacklist))
                    ev.Cancel();
            
            if (sourceRestrict.Whitelist != null &&
                !_tagSystem.HasAnyTag(user, sourceRestrict.Whitelist))
                    ev.Cancel();
            
            if (ev.Cancelled)
                _popupSystem.PopupClient(Loc.GetString("interact-restriction-restricted-source", ("item", Identity.Entity(ent, EntityManager))), user);
        }
    }
}