using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

// ReSharper disable CheckNamespace
namespace Content.Client.Options.UI;

public sealed partial class OptionsTabControlRow
{
    public OptionLineEditCvar AddOptionLineEdit(CVarDef<string> cVar, LineEdit lineEdit) =>
        AddOption(new OptionLineEditCvar(this, _cfg, cVar, lineEdit));

    public void RemoveOption(BaseOption option) => _options.Remove(option);
}
