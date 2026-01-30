using Content.Shared._Starlight.MassDriver;
using Content.Shared._Starlight.MassDriver.Components;
using Content.Shared._Starlight.MassDriver.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.MassDriver.EntitySystems;

public sealed class MassDriverSystem : SharedMassDriverSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MassDriverComponent, ComponentHandleState>(OnHandleState);
    }

    /// <summary>
    /// Handles the component state being received from the server.
    /// </summary>
    /// <param name="uid">Mass Driver</param>
    /// <param name="component">Mass Driver Component</param>
    /// <param name="args">Event args</param>
    private void OnHandleState(EntityUid uid, MassDriverComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MassDriverComponentState state)
            return;

        component.CurrentThrowSpeed = state.CurrentThrowSpeed;
        component.CurrentThrowDistance = state.CurrentThrowDistance;
        component.MaxThrowSpeed = state.MaxThrowSpeed;
        component.MaxThrowDistance = state.MaxThrowDistance;
        component.MinThrowSpeed = state.MinThrowSpeed;
        component.MinThrowDistance = state.MinThrowDistance;
        component.Mode = state.CurrentMassDriverMode;
        component.Hacked = state.Hacked;
        component.Console = GetEntity(state.Console);

        if (component.Console == null)
            return;

        _ui.ClientSendUiMessage(component.Console.Value, MassDriverConsoleUiKey.Key, new MassDriverUpdateUIMessage(state)); // Update UI on Component State
    }

    public override void ChangePowerLoad(EntityUid uid, MassDriverComponent component, float powerLoad) { } // Server side implementation only
}