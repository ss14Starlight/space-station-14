using Content.Server._Starlight.Computers.RemoteControl;
using Content.Server.Popups;
using Content.Server.Salvage.JobBoard;
using Content.Shared.Cargo.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Timing;
using Content.Shared.Cargo.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Cargo.Systems;

public sealed partial class PriceGunSystem : SharedPriceGunSystem
{
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private PricingSystem _pricingSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private CargoSystem _bountySystem = default!;
    [Dependency] private SalvageJobBoardSystem _salvageJobBoard = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    #region Starlight
    [Dependency] private RemoteControlConsoleSystem _remoteControl = default!;
    #endregion

    protected override bool GetPriceOrBounty(Entity<PriceGunComponent> entity, EntityUid target, EntityUid user)
    {
        if (!TryComp(entity.Owner, out UseDelayComponent? useDelay) || _useDelay.IsDelayed((entity.Owner, useDelay)))
            return false;

        // Starlight
        var popupRecipient = _remoteControl.TryGetControllerForRemoteEntity(user, out var controller)
            ? controller
            : user;

        // Check if we're scanning a bounty crate
        if (_bountySystem.IsBountyComplete(target, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("price-gun-bounty-complete"), user, popupRecipient); // Starlight
        }
        else if (_salvageJobBoard.FulfillsSalvageJob(target, null, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("price-gun-salvjob-complete"), user, popupRecipient); // Starlight
        }
        else // Otherwise appraise the price
        {
            var price = _pricingSystem.GetPrice(target);
            _popupSystem.PopupEntity(Loc.GetString("price-gun-pricing-result",
                    ("object", Identity.Entity(target, EntityManager)),
                    ("price", $"{price:F2}")),
                user,
                popupRecipient); // Starlight
        }

        _audio.PlayPvs(entity.Comp.AppraisalSound, entity.Owner);
        _useDelay.TryResetDelay((entity.Owner, useDelay));
        return true;
    }
}
