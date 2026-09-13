using Content.Shared._Starlight.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.StatusIcon;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using static Content.Shared._Starlight.Access.Components.JobIdentityConsoleComponent;

namespace Content.Client._Starlight.Access.UI;

public sealed partial class JobIdentityConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private IConfigurationManager _cfgManager = default!;

    private JobIdentityConsoleWindow? _window;

    private readonly int _maxIdJobLength;

    public JobIdentityConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _maxIdJobLength = _cfgManager.GetCVar(CCVars.MaxIdJobLength);
    }

    protected override void Open()
    {
        base.Open();

        var requiredTags = EntMan.GetComponent<JobIdentityConsoleComponent>(Owner).RequiredTags;

        _window = this.CreateWindow<JobIdentityConsoleWindow>();
        _window.Initialize(this, requiredTags);
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.PrivilegedIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(PrivilegedIdCardSlotId));
        _window.TargetIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(TargetIdCardSlotId));

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        var castState = (JobIdentityConsoleBoundUserInterfaceState) state;
        _window?.UpdateState(castState);
    }

    public void SubmitData(string newJobTitle, ProtoId<JobIconPrototype>? newJobIcon)
    {
        if (newJobTitle.Length > _maxIdJobLength)
            newJobTitle = newJobTitle[.._maxIdJobLength];

        SendMessage(new WriteJobIdentityMessage(newJobTitle, newJobIcon));
    }
}
