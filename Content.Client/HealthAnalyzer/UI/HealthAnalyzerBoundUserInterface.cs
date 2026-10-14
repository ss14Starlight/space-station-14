using Content.Shared.MedicalScanner;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HealthAnalyzer.UI
{
    [UsedImplicitly]
    public sealed class HealthAnalyzerBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private HealthAnalyzerWindow? _window;
        private HealthAnalyzerScannedUserMessage? _pendingMessage; // Starlight-edit: data that arrived before the window existed
        public HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<HealthAnalyzerWindow>();
            // Starlight-start: Printable health reports.
            _window.PrintReportPressed += OnPrintReportPressed;
            // Starlight-end

            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

            // Starlight-start: Show data that arrived before the window existed.
            if (_pendingMessage != null)
            {
                _window.Populate(_pendingMessage);
                _pendingMessage = null;
            }
            // Starlight-end
        }

        // Starlight-start: Printable health reports.
        private void OnPrintReportPressed()
        {
            SendMessage(new HealthAnalyzerPrintReportMessage());
        }
        // Starlight-end

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            // Starlight-start: Show data that arrived before the window existed.
            if (message is not HealthAnalyzerScannedUserMessage cast)
                return;

            if (_window == null)
            {
                _pendingMessage = cast;
                return;
            }
            // Starlight-end
            _window.Populate(cast);
        }
    }
}
