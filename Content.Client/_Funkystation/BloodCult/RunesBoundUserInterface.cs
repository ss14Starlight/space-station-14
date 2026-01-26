using Content.Client._Funkystation.BloodCult.UI;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Content.Shared._Funkystation.BloodCult.Components;

namespace Content.Client._Funkystation.BloodCult;

public sealed class RunesBoundUserInterface : BoundUserInterface
{
	[Dependency] private readonly IClyde _displayManager = default!;
	[Dependency] private readonly IInputManager _inputManager = default!;
	[Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

	private RuneRadialMenu? _runeRitualMenu;

	public RunesBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
	{
	}

	protected override void Open()
	{
		base.Open();

		_runeRitualMenu = this.CreateWindow<RuneRadialMenu>();
		_runeRitualMenu.InitializeDependencies(_entitySystemManager.DependencyCollection);
		_runeRitualMenu.SetEntity(Owner);
		_runeRitualMenu.SendRunesMessageAction += SendRunesMessage;//SendHereticRitualMessage;

		var vpSize = _displayManager.ScreenSize;
		_runeRitualMenu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);
	}

	private void SendRunesMessage(string protoId)
	{
		SendMessage(new RunesMessage(protoId));
	}
}