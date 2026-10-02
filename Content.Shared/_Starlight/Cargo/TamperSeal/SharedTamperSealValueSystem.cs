using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Cargo.TamperSeal;

/// <summary>
/// Handles rewards, penalties, and refunds of tamper-sealed orders that were unsealed or destroyed.
/// Requires a <see cref="TamperSealComponent"/> and <see cref="TamperSealValueComponent"/> to operate.
/// </summary>
public sealed partial class SharedTamperSealValueSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedCargoSystem _cargo = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealValueComponent, TamperSealOpenedEvent>(OnSealOpened);
        SubscribeLocalEvent<TamperSealValueComponent, TamperSealDestroyedEvent>(OnSealDestroyed);
    }

    private void OnSealOpened(EntityUid uid, TamperSealValueComponent value, ref TamperSealOpenedEvent args)
    {
        var stationId = value.StationId;
        var reward = new FinancialMutation(args.Seal.Deliverer, value.Reward);
        var deliverer = _proto.Index(args.Seal.Deliverer);

        if (args.Seal.Deliverer == args.Seal.Recipient) // Don't let Cargo reward themselves.
            return;
        if (!TryComp<StationBankAccountComponent>(stationId, out var bank))
            return;

        // Apply the reward if applicable.
        if (reward.Amount != 0)
            ApplyMutation(uid, (stationId, bank), reward, "opening", "rewarded", args.User);

        _audio.PlayPredicted(value.RewardSound, uid, args.User);
        _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unseal-end-reward",
                ("deliverer", Loc.GetString(deliverer.TamperSealName)),
                ("reward", reward.Amount)),
            uid, args.User, PopupType.Medium);

        // Don't show the "unseal finished" popup if we already showed a reward popup.
        args.ShowPopup = false;
    }

    private void OnSealDestroyed(EntityUid uid, TamperSealValueComponent value, ref TamperSealDestroyedEvent args)
    {
        var stationId = value.StationId;
        var penalty = new FinancialMutation(args.Seal.Deliverer, -value.Penalty);
        var refundCharge = new FinancialMutation(args.Seal.Deliverer, -value.Refund);
        var refundCredit = new FinancialMutation(args.Seal.Recipient, value.Refund);

        var deliverer = _proto.Index(args.Seal.Deliverer);
        var recipient = _proto.Index(args.Seal.Recipient);

        if (args.Seal.Deliverer == args.Seal.Recipient) // Don't let Cargo penalize/refund themselves.
            return;
        if (!TryComp<StationBankAccountComponent>(stationId, out var bank))
            return;

        // Apply the penalty if applicable. This money goes into the ether.
        if (penalty.Amount != 0)
            ApplyMutation(uid, (stationId, bank), penalty, "destroying", "penalized", args.User);

        // Apply the refund if applicable. Transfers from deliverer to recipient.
        if (value.Refund != 0)
        {
            var refundDebited = ApplyMutation(uid, (stationId, bank), refundCharge, "destroying", "charged", args.User);
            if (refundDebited)
                ApplyMutation(uid, (stationId, bank), refundCredit, "destroying", "refunded", args.User);
        }

        // Play public sound.
        if (args.ServerOnly)
        {
            // We use coordinates because some server-side triggers involve the entity being completely destroyed.
            // If we play sound on the entity or popup on the entity, they would never show up.
            var coords = Transform(uid).Coordinates;
            _audio.PlayPvs(value.PenaltySound, coords);
            _popup.PopupCoordinates(Loc.GetString("tamper-seal-popup-destroy-end-penalty",
                    ("recipient", Loc.GetString(recipient.TamperSealName)),
                    ("deliverer", Loc.GetString(deliverer.TamperSealName)),
                    ("penalty", Math.Abs(penalty.Amount))),
                coords, PopupType.LargeCaution);
        }
        else
        {
            _audio.PlayPredicted(value.PenaltySound, Transform(uid).Coordinates, args.User);
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-destroy-end-penalty",
                    ("recipient", Loc.GetString(recipient.TamperSealName)),
                    ("deliverer", Loc.GetString(deliverer.TamperSealName)),
                    ("penalty", Math.Abs(penalty.Amount))),
                uid, args.User, PopupType.LargeCaution);
        }

        // Cancel the "You destroy the seal" popup as we substituted it with the destruction popup.
        args.ShowPopup = false;
    }

    /// <summary>
    /// Applies the destruction penalty and refund to the respective bank accounts.
    /// </summary>
    /// <param name="uid">The entity that carried the seal</param>
    /// <param name="bank">The entity carrying the bank account component</param>
    /// <param name="action">Verb representing what the user did, in the form "...by {action} the seal."</param>
    /// <param name="result">The action, e.g. "penalized" or "rewarded"</param>
    /// <param name="mutation">The mutation to apply to the bank</param>
    /// <param name="user">The ID of the user that caused this (if any)</param>
    /// <returns>True when the mutation was applied.</returns>
    private bool ApplyMutation(EntityUid uid, Entity<StationBankAccountComponent?> bank,
        FinancialMutation mutation, string action, string result, EntityUid? user = null)
    {
        var adjusted = _cargo.TryAdjustBankAccount(bank, mutation.Account, mutation.Amount);

        if (adjusted)
            // Because logs are for humans, we don't really want negative numbers here.
            // It's either "rewarded 500 spesos" or "penalized 500 spesos", not "penalized -500 spesos" or whatever.
            LogMutation(uid, mutation with { Amount = Math.Abs(mutation.Amount) }, action, result, user);

        return adjusted;
    }

    /// <summary>
    /// Log a financial mutation.
    /// </summary>
    /// <param name="uid">The entity that carried the seal</param>
    /// <param name="mutation">The mutation that was applied</param>
    /// <param name="action">Verb representing what the user did, in the form "...by {action} the seal."</param>
    /// <param name="result">The action, e.g. "penalized" or "rewarded"</param>
    /// <param name="user">The ID of the user that caused this (if any)</param>
    private void LogMutation(EntityUid uid, FinancialMutation mutation,
        string action, string result, EntityUid? user)
    {
        var accountName = Loc.GetString(_proto.Index(mutation.Account).TamperSealName);

        if (user != null)
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):player} caused {accountName} to be {result} {mutation.Amount} spesos by {action} the seal on {ToPrettyString(uid)}");
        else
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"Unknown source caused {accountName} to be {result} {mutation.Amount} spesos by {action} the seal on {ToPrettyString(uid)}");
    }
}
