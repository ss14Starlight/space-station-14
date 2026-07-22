using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.ViewVariables;

namespace Content.Client._Starlight.ViewVariables;

/// <summary>
/// Adds a text editor field for LocId properties in ViewVariables.
/// </summary>
// ReSharper disable once InconsistentNaming
internal sealed class VVPropEditorLocId : VVPropEditor
{
    protected override Control MakeUI(object? value)
    {
        var locId = value is LocId l ? l : default;
        var lineEdit = new LineEdit
        {
            Text = locId.Id ?? "",
            Editable = !ReadOnly,
            HorizontalExpand = true,
        };

        if (!ReadOnly)
            lineEdit.OnTextEntered += e => ValueChanged(new LocId(e.Text));

        return lineEdit;
    }
}
