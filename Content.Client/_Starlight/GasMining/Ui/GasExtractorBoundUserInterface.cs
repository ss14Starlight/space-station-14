using Content.Shared._Starlight.GasMining.GasExtractor;
using Content.Shared.IdentityManagement;
using Content.Shared.Localizations;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.GasMining.Ui;

[UsedImplicitly]
public sealed class GasExtractorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private float _maxExtractionRate;

    [ViewVariables]
    private GasExtractorWindow? _window;

    public GasExtractorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) {}

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GasExtractorWindow>();
        if (EntMan.TryGetComponent(Owner, out GasExtractorComponent? comp))
        {
            _maxExtractionRate = comp.MaxExtractionRate;
            _window.SetMaxExtractionRate(comp.MaxExtractionRate);
        }

        _window.ToggleStatusButtonPressed += OnToggleStatusButtonPressed;
        _window.ExtractorExtractionRateChanged += OnExtractorRatePressed;
        Update();
    }

    private void OnToggleStatusButtonPressed(bool status) => SendPredictedMessage(new GasExtractorToggleStatusMessage(status));

    private void OnExtractorRatePressed(string value)
    {
        var rate = UserInputParser.TryFloat(value, out var parsed) ? parsed : 0f;
        rate = Math.Clamp(rate, 0f, _maxExtractionRate);

        SendPredictedMessage(new GasExtractorChangeExtractionRateMessage(rate));
    }

    public override void Update() {
        base.Update();

        if (_window is null || !EntMan.TryGetComponent(Owner, out GasExtractorComponent? extractor))
            return;

        _window.Title = Identity.Name(Owner, EntMan);
        _window.SetExtractorStatus(extractor.Enabled);
        _window.SetExtractionRate(extractor.ExtractionRate);
    }
}
