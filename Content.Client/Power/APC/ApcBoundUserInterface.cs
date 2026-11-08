using Content.Client.Power.APC.UI;
using Content.Shared.Access.Systems;
using Content.Shared.APC;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Power.APC
{
    [UsedImplicitly]
    public sealed partial class ApcBoundUserInterface : BoundUserInterface // Starlight-edit: Partial
    {
        [ViewVariables]
        private ApcMenu? _menu;

        public ApcBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindow<ApcMenu>();
            _menu.SetEntity(Owner);
            _menu.OnBreaker += BreakerPressed;
            _menu.OnLightingMode += LightingModePressed; // Starlight-edit

            var hasAccess = false;
            if (PlayerManager.LocalEntity != null)
            {
                var accessReader = EntMan.System<AccessReaderSystem>();
                hasAccess = accessReader.IsAllowed((EntityUid)PlayerManager.LocalEntity, Owner);
            }
            _menu?.SetAccessEnabled(hasAccess);
            UpdateLighting(); // Starlight-edit
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (ApcBoundInterfaceState) state;
            _menu?.UpdateState(castState);
        }

        public void BreakerPressed()
        {
            SendMessage(new ApcToggleMainBreakerMessage());
        }
    }
}
