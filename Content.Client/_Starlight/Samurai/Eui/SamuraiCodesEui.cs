using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared._Starlight.Samurai;

namespace Content.Client._Starlight.Samurai.Eui;

public sealed class SamuraiCodesEui : BaseEui
{
    private readonly SamuraiCodeUi _samuraiCodeUi;
    private NetEntity _target;

    public SamuraiCodesEui()
    {
        _samuraiCodeUi = new SamuraiCodeUi();
        _samuraiCodeUi.OnSave += SaveCodes;
    }

    private void SaveCodes()
    {
        var newCodes = _samuraiCodeUi.GetCodes();
        SendMessage(new SamuraiCodesSaveMessage(newCodes, _target));
        _samuraiCodeUi.SetCodes(newCodes);
    }

    public override void Opened()
        => _samuraiCodeUi.OpenCentered();

    public override void HandleState(EuiStateBase state)
    {
        if (state is not SamuraiCodesEuiState s)
            return;

        _target = s.Target;
        _samuraiCodeUi.SetCodes(s.Codes);
    }
}
