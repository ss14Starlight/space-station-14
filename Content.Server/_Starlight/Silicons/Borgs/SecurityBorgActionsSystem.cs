using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Silicons.Borgs;

public sealed class SecurityBorgActionsSystem : EntitySystem
{
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<RadioChannelPrototype> SecurityChannel = "Security";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecurityBorgComponent, BorgCallForHelpActionEvent>(OnCallForHelp);
    }

    /// <summary>
    /// Sends a radio message to the security channel with the position of the borg when they call for help.
    /// </summary>
    private void OnCallForHelp(EntityUid uid, SecurityBorgComponent _, BorgCallForHelpActionEvent args)
    {
        if (args.Handled)
            return;

        var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(uid));
        var message = Loc.GetString("borg-call-for-help-message", ("borg", uid), ("position", posText));
        _radio.SendRadioMessage(uid, message, _prototype.Index(SecurityChannel), uid);

        args.Handled = true;
    }
}
