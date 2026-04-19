using Content.Shared._Starlight.GameTicking.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Database;
using Content.Shared.Emag.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using PreventEorgComponent = Content.Shared._Starlight.EndOfRoundGriefing.Components.PreventEorgComponent;

namespace Content.Shared.Emag.Systems;

/// How to add an emag interaction:
/// 1. Go to the system for the component you want the interaction with
/// 2. Subscribe to the GotEmaggedEvent
/// 3. Have some check for if this actually needs to be emagged or is already emagged (to stop charge waste)
/// 4. Past the check, add all the effects you desire and HANDLE THE EVENT ARGUMENT so a charge is spent
/// 5. Optionally, set Repeatable on the event to true if you don't want the emagged component to be added
public sealed class EmagSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmagComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EmaggedComponent, OnAccessOverriderAccessUpdatedEvent>(OnAccessOverriderAccessUpdated);
    }

    private void OnAccessOverriderAccessUpdated(Entity<EmaggedComponent> entity, ref OnAccessOverriderAccessUpdatedEvent args)
    {
        if (!CompareFlag(entity.Comp.EmagType, EmagType.Access))
            return;

        entity.Comp.EmagType &= ~EmagType.Access;
        Dirty(entity);
    }
    private void OnAfterInteract(EntityUid uid, EmagComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryEmagEffect((uid, comp), args.User, target);
    }

    /// <summary>
    /// Does the emag effect on a specified entity with a specified EmagType. The optional field customEmagType can be used to override the emag type defined in the component.
    /// </summary>
    public bool TryEmagEffect(Entity<EmagComponent?> ent, EntityUid user, EntityUid target, EmagType? customEmagType = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (_tag.HasTag(target, ent.Comp.EmagImmuneTag))
            return false;

        if (HasComp<PreventEorgComponent>(user)) // Starlight BEGIN
        {
            _popup.PopupClient(Loc.GetString("eorg-action"), user, PopupType.LargeCaution);
            return false;
        } // Starlight END

        Entity<LimitedChargesComponent?> chargesEnt = ent.Owner;
        if (_sharedCharges.IsEmpty(chargesEnt))
        {
            _popup.PopupClient(Loc.GetString("emag-no-charges"), user, user);
            return false;
        }

        var typeToUse = customEmagType ?? ent.Comp.EmagType;

        var emaggedEvent = new GotEmaggedEvent(user, typeToUse, EmagComponent: ent.Comp);
        RaiseLocalEvent(target, ref emaggedEvent);

        if (!emaggedEvent.Handled)
            return false;

        _popup.PopupPredicted(Loc.GetString("emag-success", ("target", Identity.Entity(target, EntityManager))), user, user, PopupType.Medium);

        _audio.PlayPredicted(ent.Comp.EmagSound, ent, ent);

        _adminLogger.Add(LogType.Emag, LogImpact.High, $"{ToPrettyString(user):player} emagged {ToPrettyString(target):target} with flag(s): {typeToUse}");

        if (emaggedEvent.Handled)
            _sharedCharges.TryUseCharge(chargesEnt);

        // Starlight begin
        EnsureComp<EmaggedComponent>(target, out var emaggedComp);
        emaggedComp.OwningFaction = ent.Comp.OwningFaction;
        Dirty(target, emaggedComp);

        if (!emaggedEvent.Repeatable)
        {
            emaggedComp.EmagType |= typeToUse;
            Dirty(target, emaggedComp);
        }
        // Starlight end

        return emaggedEvent.Handled;
    }

    // Starlight begin
    /// <summary>
    /// Checks whether an entity has the EmaggedComponent with a set flag.
    /// </summary>
    /// <param name="target">The target entity to check for the flag.</param>
    /// <param name="flag">The EmagType flag to check for.</param>
    /// <param name="emag">The component of the emag being used. If specified, will bypass the check if the emag factions differ.</param>
    /// <returns>True if entity has EmaggedComponent and the provided flag. False if the entity lacks EmaggedComponent or provided flag.</returns>
    public bool CheckFlag(EntityUid target, EmagType flag, EmagComponent? emag = null)
    {
        if (!TryComp<EmaggedComponent>(target, out var comp))
            return false;

        if ((comp.EmagType & flag) == flag)
        {
            if (emag is null) return true;
            return emag.OwningFaction == comp.OwningFaction;
        }

        return false;
    }
    // Starlight end

    /// <summary>
    /// Compares a flag to the target.
    /// </summary>
    /// <param name="target">The target flag to check.</param>
    /// <param name="flag">The flag to check for within the target.</param>
    /// <returns>True if target contains flag. Otherwise false.</returns>
    public bool CompareFlag(EmagType target, EmagType flag)
    {
        if ((target & flag) == flag)
            return true;

        return false;
    }
}


[Flags]
[Serializable, NetSerializable]
public enum EmagType
{
    None = 0,
    All = ~None,
    Interaction = 1 << 1,
    Access = 1 << 2
}
/// <summary>
/// Shows a popup to emag user (client side only!) and adds <see cref="EmaggedComponent"/> to the entity when handled
/// </summary>
/// <param name="UserUid">Emag user</param>
/// <param name="Type">The emag type to use</param>
/// <param name="Handled">Did the emagging succeed? Causes a user-only popup to show on client side</param>
/// <param name="Repeatable">Can the entity be emagged more than once? Prevents adding of <see cref="EmaggedComponent"/></param>
/// <remarks>Needs to be handled in shared/client, not just the server, to actually show the emagging popup</remarks>
[ByRefEvent]
public record struct GotEmaggedEvent(EntityUid UserUid, EmagType Type, EmagComponent? EmagComponent = null, bool Handled = false, bool Repeatable = false); // Starlight
