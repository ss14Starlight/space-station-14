// Afterlight
using Content.Shared._Afterlight.Silicons.Borgs; // Afterlight
using Content.Shared.Silicons.Borgs.Components; // Afterlight
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

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<BorgSelectTypeMenu>();
        _menu.ConfirmedBorgType += (prototype, subtypePrototype) => SendMessage(new BorgSelectSubtypeMessage(prototype, subtypePrototype?.ID)); // Afterlight - borg subtypes - Starlight
        _menu.SetupMenu(Owner); // Starlight-edit
    }
}
