using Content.Shared._Starlight.DocumentManager;
using Content.Shared._Starlight.Fax;
using Content.Shared._Starlight.Fax.UI;
using Content.Shared._Starlight.Paper;
using Content.Shared._Starlight.Time;
using Content.Shared._Starlight.Utility;
using Content.Shared.DeviceNetwork;
using Content.Shared.Emag.Components;
using Content.Shared.Fax.Components;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Server.Fax;

public sealed partial class FaxSystem
{
    [Dependency] private InventorySystem _inventory = null!;
    [Dependency] private SharedTimeSystem _time = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private PreWrittenDocumentManager _documentManager = null!;
    [Dependency] private IComponentFactory _factory = null!;

    private string GetTimeStamp()
    {
        var date = _time.GetDate();
        var time = _time.GetShiftDuration();
        return string.Format($"{date} {time:hh\\:mm}");
    }

    private static string StripContentMetadata(string content)
    {
        var parsed = new FormattedMessage();
        parsed.AddMarkupPermissive(content);
        return parsed.RemoveLeading(["meta"]).ToMarkup();
    }

    private string PrependContentMetadata(string content, FaxPrintout payload, FaxMachineComponent comp)
    {
        const string MetaFormat = """
        [meta][dots bold]Sent: {0} at {1}
        Rcvd: {2} at {3}[/dots]
        [/meta]{4}
        """;

        return string.Format(MetaFormat, payload.MetaSentAt, FormattedMessage.EscapeText(payload.MetaSender ?? ""),
            TimeSpan.FromSeconds(Math.Truncate(_gameTicker.RoundDuration().TotalSeconds)).ToString(), FormattedMessage.EscapeText(comp.FaxName ?? ""), content);
    }

    private FaxPrintout? TryGetFaxablePrintout(EntityUid? item, FaxMachineComponent component)
    {
        if (item is not { } sendEntity ||
            !TryComp<FaxableObjectComponent>(sendEntity, out var faxable) ||
            string.IsNullOrEmpty(faxable.OutputtingText))
            return null;

        return !_documentManager.TryGetDocumentContents(faxable.OutputtingText, out var text)
            ? null
            : new FaxPrintout(
                text,
                Loc.GetString("fax-machine-printed-paper-name"),
                prototypeId: component.PrintPaperId,
                retainMetadata: true);
    }

    private bool SendFaxablePrintout(EntityUid uid, FaxMachineComponent component)
    {
        var printout = TryGetFaxablePrintout(component.PaperSlot.Item, component);
        if (printout == null)
            return false;

        if (component.SendTimeoutRemaining > 0) return false;

        if (component.DestinationFaxAddress == null ||
            !component.KnownFaxes.ContainsKey(component.DestinationFaxAddress))
            return false;

        var payload = new NetworkPayload()
        {
            { DeviceNetworkConstants.Command, FaxConstants.FaxPrintCommand },
            { FaxConstants.FaxPaperNameData, printout.Name },
            { FaxConstants.FaxPaperContentData, printout.Content },
            { FaxConstants.FaxPaperPrototypeData, printout.PrototypeId },
            { FaxConstants.FaxPaperLockedData, false },
            { FaxConstants.FaxMetaSender, component.FaxName },
            { FaxConstants.FaxMetaSentAt, GetTimeStamp() }
        };

        _deviceNetworkSystem.QueuePacket(uid, component.DestinationFaxAddress, payload);
        _audioSystem.PlayPvs(component.SendSound, uid);
        component.SendTimeoutRemaining += component.SendTimeout;
        UpdateUserInterface(uid, component);
        return true;
    }

    private void UpdateMachineConfigureUserInterface(EntityUid uid, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new FaxMachineConfigureState(component.FaxName, component.CurrentGroup,
            component.IntrinsicGroup, component.IntrinsicLocked,
            component.Order, HasComp<EmaggedComponent>(uid));
        _userInterface.SetUiState(uid, FaxMachineConfigureUiKey.Key, state);
    }

    private void OnConfigure(EntityUid uid, FaxMachineComponent component, FaxMachineConfigureMessage args)
    {
        component.FaxName = args.Name;
        component.CurrentGroup = args.Grouping;
        component.Order = args.Order;

        _popupSystem.PopupEntity(Loc.GetString("fax-machine-configure-ui-saved"), uid, args.Actor);
        UpdateUserInterface(uid, component);
        UpdateMachineConfigureUserInterface(uid, component);
    }

    // Methods to send fax prototypes or with custom data.
    public void SendFaxWithData(EntityUid targetFaxUid, EntProtoId faxablePrototype, PaperData? data = null)
    {
        if (!_proto.TryIndex(faxablePrototype, out var fax))
            return;
        if (!fax.HasComp<FaxableObjectComponent>(_factory) || !fax.TryComp<PaperComponent>(out var paper, _factory))
            throw new Exception($"Entity prototype {faxablePrototype.Id} is not faxable.");

        var content = data?.Content ?? paper.Content;

        if (fax.TryComp<TextFilePaperContentComponent>(out var textComp, _factory))
            _documentManager.TryGetDocumentContents(textComp.FileName, out content);

        data = new PaperData
        {
            Name = data?.Name ?? fax.Name,
            Content = content,
            StampState = data?.StampState ?? paper.StampState,
            StampedBy = data?.StampedBy ?? paper.StampedBy,
            EditingDisabled = data?.EditingDisabled ?? paper.EditingDisabled,
            SentBy = data?.SentBy,
            IncludeMeta = data?.IncludeMeta ?? true
        };

        SendFaxWithData(targetFaxUid, data, faxablePrototype);
    }

    public void SendFaxWithData(EntityUid targetFaxUid, PaperData data, EntProtoId? paperProtoId = null)
    {
        if (!TryComp<FaxMachineComponent>(targetFaxUid, out var comp)) return;
        paperProtoId ??= comp.PrintPaperId;
        if (!_proto.TryIndex(paperProtoId, out var proto))
            return;

        var printout = new FaxPrintout(data.Content ?? "", data.Name ?? proto.Name, null, paperProtoId,
            data.StampState, data.StampedBy, data.EditingDisabled, data.SentBy, metaSender: data.IncludeMeta ? data.SentBy : null,
            metaSentAt: data.IncludeMeta ? GetTimeStamp() : null, includeMetadata: data.IncludeMeta);

        Receive(targetFaxUid, printout);
    }
}
