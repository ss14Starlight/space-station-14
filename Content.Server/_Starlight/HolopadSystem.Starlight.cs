using Content.Shared.Holopad;
using Content.Shared.Labels.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Telephone;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Server.Holopad;

public sealed partial class HolopadSystem
{
    private bool IsHolopadContactUiOpen(EntityUid uid)
    {
        var uiKey = HasComp<StationAiCoreComponent>(uid)
            ? HolopadUiKey.AiActionWindow
            : HolopadUiKey.InteractionWindow;

        return _userInterfaceSystem.IsUiOpen(uid, uiKey);
    }

    private List<HolopadUiContact> BuildHolopadUiContacts()
    {
        var contacts = new List<HolopadUiContact>();
        var query = AllEntityQuery<HolopadComponent, TelephoneComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var telephone, out var xform))
        {
            if (telephone.UnlistedNumber)
                continue;

            var name = MetaData(uid).EntityName;
            if (TryComp<LabelComponent>(uid, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
                name = label.CurrentLabel;

            contacts.Add(new HolopadUiContact(
                uid,
                name,
                xform.MapID,
                xform.GridUid,
                telephone.TransmissionRange));
        }

        return contacts;
    }

    private void UpdateHolopadUIState(
        Entity<HolopadComponent> entity,
        TelephoneComponent telephone,
        TransformComponent xform,
        List<HolopadUiContact> contacts)
    {
        var holopads = new Dictionary<NetEntity, string>(contacts.Count);

        foreach (var contact in contacts)
        {
            if (contact.Uid == entity.Owner)
                continue;

            if (!_telephoneSystem.IsSourceInRangeOfReceiver(
                    telephone,
                    xform.MapID,
                    xform.GridUid,
                    contact.TransmissionRange,
                    contact.MapId,
                    contact.GridUid))
                continue;

            holopads.Add(GetNetEntity(contact.Uid), contact.Name);
        }

        var uiKey = HasComp<StationAiCoreComponent>(entity)
            ? HolopadUiKey.AiActionWindow
            : HolopadUiKey.InteractionWindow;
        _userInterfaceSystem.SetUiState(entity.Owner, uiKey, new HolopadBoundInterfaceState(holopads));
    }

    private readonly record struct HolopadUiContact(
        EntityUid Uid,
        string Name,
        MapId MapId,
        EntityUid? GridUid,
        TelephoneRange TransmissionRange);
}
