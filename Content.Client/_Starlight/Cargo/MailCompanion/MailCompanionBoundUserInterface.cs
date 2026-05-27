using Content.Shared._Starlight.Cargo.MailCompanion;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Cargo.MailCompanion;

[UsedImplicitly]
public sealed class MailCompanionBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MailCompanionWindow? _window;

    protected override void Open()
    {
        base.Open();

        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
                stationName = metaData.EntityName;
        }

        _window = this.CreateWindow<MailCompanionWindow>();
        _window.Set(stationName, gridUid);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is MailCompanionState companionState)
            _window?.UpdateState(companionState, Owner);
    }
}
