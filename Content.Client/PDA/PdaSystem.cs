using Content.Shared.PDA;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Linq;
using Robust.Shared.Log;

namespace Content.Client.PDA;

public sealed class PdaSystem : SharedPdaSystem
{

    // <summary>
    // Starlight-start: PDA Popout
    // This is a beta version of the PDA popout system.
    // It pops out a window from the original game window and creates a new OS window.
    // </summary>

    /* 
        The problem with this implementation is that there are 3-4 ways to close a window.
        It's easy to pop out the PDA. But to keep track of the popout and make sure that it
        behaves consistent and is always closed,
        no matter from what way of closing you chose is very hard.

        According to Rinary, this needs a proper implementation including client/server communication.
        This is planned to do in a different interation.
        In the mean while this is released in a semi-buggy state.

        Opening the popout usually works very well.
        Closing can be an issue. A user will find out that
        they have to close the OS window for the best result.

        Known issues:
        - The popout window remains open and black when the PDA is closed.
            Generally the popout window has to be closed manually on the OS window frame.
        - A second invocation of "Toggle UI" on the PDA context menu will not work and
        have to be triggered another time
        - Not introduced but became with this feature more obvious :
            The Crew Monitor doesn't update by itself. So even if it's not popped out,
            it have to be closed and reopened to get the latest data. I would advice a
            running timer when the Crew Monitor is open which updates every 5 seconds.
    */

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    
    private readonly ISawmill _sawmill = Logger.GetSawmill("PdaSystem");

    private PdaMenu? _popoutMenu;
    private IClydeWindow? ClydeWindow;
    private WindowRoot? WindowRoot;
    
    public override void Initialize()
    {
        base.Initialize();
    }

    public void OnPdaPopout(PdaMenu menu)
    {
        // Create a new popout window
        CreatePopout(menu);
    }
        
    private void CreatePopout(PdaMenu menu)
    {
        // Orphan the menu from its current parent
        menu.Orphan();
        
        // Get the second monitor as the primary monitor is the game window
        // Or why else should someone want to pop out something if they
        // aren't using at least 2 monitors/displays?
        // Doesn't seem to work though
        var monitor = _clyde.EnumerateMonitors().Skip(1).First();

        // Create a new window        
        ClydeWindow = _clyde.CreateWindow(new WindowCreateParameters
        {
            Maximized = false,
            Title = "PDA",
            Monitor = monitor,
            Width = 576,
            Height = 450
        });
        
        ClydeWindow.RequestClosed += OnWindowClosed;
        ClydeWindow.DisposeOnClose = true;
        
        // Create a window root and add the menu to it
        WindowRoot = _uiManager.CreateWindowRoot(ClydeWindow);
        WindowRoot.AddChild(menu);
        
        // Store the menu for later
        _popoutMenu = menu;
        
        // Disable the popout button in the popout window
        menu.PopoutButton.Disabled = true;
        menu.PopoutButton.Visible = false;
    }
    
    private void OnWindowClosed(WindowRequestClosedEventArgs args)
    {
        ClosePopout();
    }
    
    private void ClosePopout()
    {
        if (_popoutMenu == null || ClydeWindow == null || WindowRoot == null)
            return;
        
        try
        {
            // Remove the menu from the window root
            _popoutMenu.Orphan();
            
            // Dispose the window
            ClydeWindow.Dispose();
        }
        catch (Exception e)
        {
            _sawmill.Error("Error closing popout window: {Error}", e);    
        }
        finally
        {
            ClydeWindow = null;
            WindowRoot = null;
            _popoutMenu = null;
        }
    }
    // Starlight-end
}
