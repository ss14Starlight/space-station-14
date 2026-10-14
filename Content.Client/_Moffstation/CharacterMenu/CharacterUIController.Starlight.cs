using System;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client._Moffstation.CharacterMenu;

public sealed partial class CharacterUIController
{
    /// <summary>
    /// Wraps styled words into centered rows while preserving the role's font and color.
    /// </summary>
    private static void SetCenteredText(WrapContainer container, string text, Color color, LocId format, string argument)
    {
        container.RemoveAllChildren();
        foreach (var word in text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
        {
            container.AddChild(new RichTextLabel
            {
                Text = Loc.GetString(format,
                    ("color", color.ToHex()),
                    (argument, FormattedMessage.EscapeText(word))),
            });
        }
    }
}
