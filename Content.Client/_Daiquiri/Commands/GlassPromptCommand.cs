using Content.Client._Daiquiri.Options.UI;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client._Daiquiri.Commands;

/// <summary>
/// Opens the first-run liquid glass prompt (for testing the newcomer flow).
/// </summary>
public sealed class GlassPromptCommand : IConsoleCommand
{
    public string Command => "glassprompt";
    public string Description => "Open the liquid glass first-run prompt window.";
    public string Help => "glassprompt";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        GlassThemePrompt.Open(IoCManager.Resolve<IUserInterfaceManager>());
    }
}
