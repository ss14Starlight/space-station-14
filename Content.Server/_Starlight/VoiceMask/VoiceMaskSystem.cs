using Content.Shared._Starlight.Radio;
using Content.Shared.Database;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.StatusIcon;
using Content.Shared.VoiceMask;

namespace Content.Server.VoiceMask;

public sealed partial class VoiceMaskSystem
{
    [SubscribeLocalEvent]
    private void OnChangeRadioIcon(Entity<VoiceMaskComponent> entity, ref VoiceMaskChangeRadioIconMessage msg)
    {
        if (msg.JobIcon is { } id
            && (!_proto.TryIndex<JobIconPrototype>(id, out var icon) || !icon.AllowSelection))
            return;

        entity.Comp.RadioJobIcon = msg.JobIcon;
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(msg.Actor):player} set radio job icon of {ToPrettyString(entity):mask}: {msg.JobIcon?.Id ?? "none"}");

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-popup-radio-icon-success"), entity, msg.Actor);

        UpdateUI(entity);
    }

    [SubscribeLocalEvent]
    private void OnGetRadioJobIconInventory(Entity<VoiceMaskComponent> entity, ref InventoryRelayedEvent<GetRadioJobIconEvent> args)
        => GetRadioJobIcon(entity, args.Args);

    [SubscribeLocalEvent]
    private void OnGetRadioJobIconImplant(Entity<VoiceMaskComponent> entity, ref ImplantRelayEvent<GetRadioJobIconEvent> args)
        => GetRadioJobIcon(entity, args.Args);

    private static void GetRadioJobIcon(Entity<VoiceMaskComponent> entity, GetRadioJobIconEvent args)
    {
        if (!entity.Comp.Active || entity.Comp.RadioJobIcon is not { } icon)
            return;

        args.JobIcon = icon;
    }
}
