using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewManifest;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.IdCardConsoleComponent;
using Robust.Client.UserInterface;
using Content.Shared._Starlight.Access;

namespace Content.Client.Access.UI
{
    public sealed partial class IdCardConsoleBoundUserInterface : BoundUserInterface
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IConfigurationManager _cfgManager = default!;
        private readonly SharedIdCardConsoleSystem _idCardConsoleSystem = default!;

        private IdCardConsoleWindow? _window;

        // CCVar.
        private int _maxNameLength;
        private int _maxIdJobLength;

        public IdCardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _idCardConsoleSystem = EntMan.System<SharedIdCardConsoleSystem>();

            _maxNameLength =_cfgManager.GetCVar(CCVars.MaxNameLength);
            _maxIdJobLength = _cfgManager.GetCVar(CCVars.MaxIdJobLength);
        }

        protected override void Open()
        {
            base.Open();
            // Starlight-edit: Start
            var requiredTags = EntMan.GetComponent<IdCardConsoleComponent>(Owner).RequiredTags;

            _window = this.CreateWindow<IdCardConsoleWindow>();
            _window.Initialize(this, requiredTags);
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
            // Starlight-edit: End
            _window.CrewManifestButton.OnPressed += _ => SendMessage(new CrewManifestOpenUiMessage());
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
            var castState = (IdCardConsoleBoundUserInterfaceState) state;
            _window?.UpdateState(castState);
        }
        // Starlight-edit: Start
        public void SubmitData(string newFullName, string newJobTitle, List<ProtoId<AccessLevelPrototype>> newAccessList, ProtoId<JobPrototype>? newJobPrototype, ProtoId<JobIconPrototype>? newJobIcon) // Starlight: Nullable newJobPrototype, newJobIcon
        {
            if (newFullName.Length > _maxNameLength)
                newFullName = newFullName[.._maxNameLength];

            if (newJobTitle.Length > _maxIdJobLength)
                newJobTitle = newJobTitle[.._maxIdJobLength];

            SendMessage(new WriteToTargetIdMessage(
                newFullName,
                newJobTitle,
                newAccessList,
                newJobPrototype,
                newJobIcon));
        }
        // Starlight-edit: End

        public void OnGroupSelected(ProtoId<AccessGroupPrototype> group)
        {
            SendMessage(new IdCardConsoleComponent.AccessGroupSelectedMessage(group)); // Starlight-edit
        }
    }
}
