// ReSharper disable CheckNamespace
using Content.Shared.Communications;

namespace Content.Server.Communications;

public sealed partial class CommunicationsConsoleSystem
{
    [SubscribeLocalEvent]
    private void OnUIOpened(Entity<CommunicationsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(CommunicationsConsoleUiKey.Key))
            UpdateCommsConsoleInterface(ent.Owner, ent.Comp);
    }
}

