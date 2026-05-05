using Content.Shared._Starlight.Cargo.MailCompanion;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._Starlight.Cargo.MailCompanion;

[UsedImplicitly]
public sealed class MailCompanionBoundUserInterface : BoundUserInterface
{
    private MailCompanionWindow? _window;

    public MailCompanionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

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
