using Content.Shared.Interaction.Events;
using Content.Shared.PAI;
using Content.Shared.PDA;
using Content.Shared.UserInterface;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.PAI;

public sealed partial class PAISystem
{
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;

    private const string PaiConsoleSlotId = "pai_slot";

    private void InitializePAISlotting()
    {
        SubscribeLocalEvent<PAIComponent, PAIPDAActionEvent>(OnOpenPda);
        SubscribeLocalEvent<PAIComponent, PAIConsoleActionEvent>(OnOpenConsole);
    }

    private void OnOpenPda(Entity<PAIComponent> ent, ref PAIPDAActionEvent args)
    {
        if (!_containerSystem.TryGetContainingContainer(ent.Owner, out var container) || container == null)
            return;

        if (container.ID != PdaComponent.PdaPaiSlotId || !HasComp<PdaComponent>(container.Owner))
            return;

        _userInterface.TryToggleUi(container.Owner, PdaUiKey.Key, ent.Owner);
    }

    private void OnOpenConsole(Entity<PAIComponent> ent, ref PAIConsoleActionEvent args)
    {
        if (!_containerSystem.TryGetContainingContainer(ent.Owner, out var container) || container == null)
            return;

        if (container.ID != PaiConsoleSlotId ||
            !TryComp<ActivatableUIComponent>(container.Owner, out var activatableUi) ||
            !TryComp<UserInterfaceComponent>(container.Owner, out var uiComp) ||
            !TryComp<ActorComponent>(ent.Owner, out var actor) ||
            activatableUi.Key == null)
        {
            return;
        }

        var wasOpen = _userInterface.IsUiOpen(container.Owner, activatableUi.Key, ent.Owner);

        if (!wasOpen)
        {
            if (activatableUi.SingleUser && activatableUi.CurrentSingleUser != null && ent.Owner != activatableUi.CurrentSingleUser)
            {
                var message = Loc.GetString("machine-already-in-use", ("machine", container.Owner));
                _popup.PopupClient(message, container.Owner, ent.Owner);
                return;
            }

            var userAttempt = new UserOpenActivatableUIAttemptEvent(ent.Owner, container.Owner, false);
            RaiseLocalEvent(ent.Owner, userAttempt);
            if (userAttempt.Cancelled)
                return;

            var openAttempt = new ActivatableUIOpenAttemptEvent(ent.Owner, false);
            RaiseLocalEvent(container.Owner, openAttempt);
            if (openAttempt.Cancelled)
                return;

            RaiseLocalEvent(container.Owner, new BeforeActivatableUIOpenEvent(ent.Owner));

            if (activatableUi.SingleUser)
            {
                activatableUi.CurrentSingleUser = ent.Owner;
                Dirty(container.Owner, activatableUi);
                RaiseLocalEvent(container.Owner, new ActivatableUIPlayerChangedEvent());
            }
        }

        if (!_userInterface.TryToggleUi((container.Owner, uiComp), activatableUi.Key, actor.PlayerSession))
            return;

        if (wasOpen)
            return;

        if (!_userInterface.IsUiOpen(container.Owner, activatableUi.Key, ent.Owner))
        {
            if (activatableUi.SingleUser && activatableUi.CurrentSingleUser == ent.Owner)
            {
                activatableUi.CurrentSingleUser = null;
                Dirty(container.Owner, activatableUi);
                RaiseLocalEvent(container.Owner, new ActivatableUIPlayerChangedEvent());
            }

            return;
        }

        RaiseLocalEvent(container.Owner, new AfterActivatableUIOpenEvent(ent.Owner));
    }
}