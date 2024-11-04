using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Starlight.MHelp.UI;

[GenerateTypedNameReferences]
public sealed partial class MentorWindow : DefaultWindow
{
    public MentorWindow()
    {
        RobustXamlLoader.Load(this);

        MHelpControl.TicketSelector.OnSelectionChanged += ticket =>
        {
            if (ticket is null)
            {
                Title = Loc.GetString("bwoink-title-none-selected");
                return;
            }

            Title = ticket.Value.ToString();
        };

        OnOpen += MHelpControl.UpdateList;
    }
}
