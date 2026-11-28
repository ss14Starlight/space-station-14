// Afterlight
using Content.Shared._Afterlight.Silicons.Borgs; // Afterlight
using Content.Shared.Silicons.Borgs.Components; // Afterlight
using Content.Client._Starlight.Computers.RemoteControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// User interface used by borgs to select their type.
/// </summary>
/// <seealso cref="BorgSelectTypeMenu"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
/// <seealso cref="BorgSwitchableTypeUiKey"/>
[UsedImplicitly]
public sealed class BorgSelectTypeUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BorgSelectTypeMenu? _menu;
    private RemoteControlInterface _remoteControl = default!; // Starlight

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _remoteControl = EntMan.System<RemoteControlInterface>(); // Starlight
        _remoteControl.ControlledEntityChanged += OnControlledEntityChanged; // Starlight

        _menu = this.CreateWindow<BorgSelectTypeMenu>();
        // Starlight - start
        _menu.ConfirmedBorgType += (prototype, subtypePrototype) =>
        {
            var message = new BorgSelectSubtypeMessage(prototype, subtypePrototype?.ID);
            if (_remoteControl.ControlledEntity == Owner)
                SendPredictedMessage(message);
            else
                SendMessage(message);
        }; // Afterlight - borg subtypes - Starlight
        // Starlight - end
        _menu.SetupMenu(Owner); // Starlight-edit
    }

    private void OnControlledEntityChanged(EntityUid? entity) => TryEmbedMenu(); // Starlight

    private void TryEmbedMenu() // Starlight
    {
        if (_menu is not null)
            _remoteControl.TryEmbedWindow(Owner, _menu);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _remoteControl.ControlledEntityChanged -= OnControlledEntityChanged;

        base.Dispose(disposing);
    }
}
