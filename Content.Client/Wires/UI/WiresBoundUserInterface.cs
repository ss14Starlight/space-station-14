using Content.Shared.Wires;
using Robust.Client.UserInterface;

namespace Content.Client.Wires.UI
{
    public sealed partial class WiresBoundUserInterface : BoundUserInterface // Starlight
    {
        [ViewVariables]
        private WiresMenu? _menu;

        public WiresBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) => IoCManager.InjectDependencies(this); // Starlight

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindow<WiresMenu>();
            // Starlight - start
            _menu.SetStationAiInteraction(_isStationAiUser);
            // Starlight - end
            _menu.OnAction += PerformAction;
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            _menu?.Populate((WiresBoundUserInterfaceState) state);
        }

        public void PerformAction(int id, WiresAction action)
        {
            SendMessage(new WiresActionMessage(id, action));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            _menu?.Dispose();
        }
    }
}
