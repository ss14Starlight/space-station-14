using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Chemistry.UI;

public sealed class ReagentButton : Button
{
    public ChemMasterReagentAmount Amount { get; set; }
    public readonly bool IsBuffer;
    public ReagentId Id { get; }
    public ReagentButton(string text, ChemMasterReagentAmount amount, ReagentId id, bool isBuffer, string styleClass)
    {
        AddStyleClass(styleClass);
        Text = text;
        Amount = amount;
        Id = id;
        IsBuffer = isBuffer;
    }
}
