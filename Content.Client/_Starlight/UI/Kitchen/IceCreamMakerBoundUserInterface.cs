using Content.Shared.Chemistry.Reagent;
using Content.Shared.Kitchen;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UI.Kitchen
{
    /// <summary>
    /// IcecreamMakerBoundUserInterface is a modified version of MicrowaveBoundUserInterface that reduces the buttons to only a start, stop, and eject button.
    /// It will use IcecreamMakerMenu.xaml.cs and .xaml which are also based on the Microwave versions.
    /// </summary>
    [UsedImplicitly]
    public sealed class IceCreamMakerBoundUserInterface : BoundUserInterface
    {

        [ViewVariables]
        private EntityUid? _owner;

        [ViewVariables]
        private IceCreamMakerMenu? _menu;

        [ViewVariables]
        private readonly Dictionary<int, EntityUid> _solids = new();

        [ViewVariables]
        private readonly Dictionary<int, ReagentQuantity> _reagents = new();

        public IceCreamMakerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _owner = owner;
            IoCManager.InjectDependencies(this);
        }

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindow<IceCreamMakerMenu>();

            if (!EntMan.TryGetComponent<CookingDeviceComponent>(_owner, out var cookingDevice))
                return;

            _menu.StopButton.OnPressed += _ => SendPredictedMessage(new MicrowaveStopCookMessage());
            _menu.StopButton.Visible = false;

            _menu.StartButton.OnPressed += _ =>
            {
                // Since there's only one button, use 0 for buttonindex. Same with the time, only 5 seconds exists in the UI.
                var defaultButton = 0;
                uint defaultTime = 5;

                SendPredictedMessage(new MicrowaveSelectCookTimeMessage(defaultButton, defaultTime));
                SendPredictedMessage(new MicrowaveStartCookMessage());
            };
            _menu.EjectButton.OnPressed += _ => SendPredictedMessage(new MicrowaveEjectMessage());
            _menu.IngredientsList.OnItemSelected += args =>
            {
                SendPredictedMessage(new MicrowaveEjectSolidIndexedMessage(EntMan.GetNetEntity(_solids[args.ItemIndex])));
            };

        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            if (state is not MicrowaveUpdateUserInterfaceState cState || _menu == null)
            {
                return;
            }

            _menu.IsBusy = cState.IsMicrowaveBusy;
            _menu.IsSafe = cState.IsMicrowaveSafe;
            _menu.CurrentCooktimeEnd = cState.CurrentCookTimeEnd;

            _menu.ToggleBusyDisableOverlayPanel(cState.IsMicrowaveBusy || cState.ContainedSolids.Length == 0);
            // TODO move this to a component state and ensure the net ids.
            RefreshContentsDisplay(EntMan.GetEntityArray(cState.ContainedSolids));

            _menu.StartButton.Disabled = cState.IsMicrowaveBusy || cState.ContainedSolids.Length == 0;
            _menu.StartButton.Visible = !cState.IsMicrowaveBusy;
            _menu.StopButton.Visible = cState.IsMicrowaveBusy;
            _menu.StopButton.Disabled = !cState.IsMicrowaveBusy;
            _menu.EjectButton.Disabled = cState.IsMicrowaveBusy || cState.ContainedSolids.Length == 0;

            _menu.StartedCooktime = cState.StartedCookTime;

            if (cState.StartedCookTime != TimeSpan.Zero)
                _menu.CurrentCookTimeInfoLabel.Text = Loc.GetString("microwave-bound-user-interface-current-cook-time-label", ("time", (cState.CurrentCookTimeEnd - cState.StartedCookTime).ToString(@"mm\:ss")));
            else
                _menu.CurrentCookTimeInfoLabel.Text = Loc.GetString("microwave-bound-user-interface-current-cook-time-label", ("time", cState.StartedCookTime.ToString(@"mm\:ss")));

            foreach (Button children in _menu.CookTimeButtonVbox.Children)
            {
                children.Disabled = cState.IsMicrowaveBusy;
            }

            //Set the "micowave light" ui color to indicate if the microwave is busy or not
            if (cState.IsMicrowaveBusy && cState.ContainedSolids.Length > 0)
            {
                _menu.IngredientsPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#947300") };
            }
            else
            {
                _menu.IngredientsPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1B1B1E") };
            }
        }

        private void RefreshContentsDisplay(EntityUid[] containedSolids)
        {
            _reagents.Clear();

            if (_menu == null) return;

            _solids.Clear();
            _menu.IngredientsList.Clear();
            foreach (var entity in containedSolids)
            {
                if (EntMan.Deleted(entity))
                {
                    return;
                }

                Texture? texture;
                if (EntMan.TryGetComponent<IconComponent>(entity, out var iconComponent))
                {
                    texture = EntMan.System<SpriteSystem>().GetIcon(iconComponent);
                }
                else if (EntMan.TryGetComponent<SpriteComponent>(entity, out var spriteComponent))
                {
                    texture = spriteComponent.Icon?.Default;
                }
                else
                {
                    continue;
                }

                var solidItem = _menu.IngredientsList.AddItem(EntMan.GetComponent<MetaDataComponent>(entity).EntityName, texture);
                var solidIndex = _menu.IngredientsList.IndexOf(solidItem);
                _solids.Add(solidIndex, entity);
            }
        }
    }
}
