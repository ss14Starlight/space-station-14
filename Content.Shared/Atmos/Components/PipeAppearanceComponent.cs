using Robust.Shared.Utility;

namespace Content.Shared.Atmos.Components;

[RegisterComponent]
public sealed partial class PipeAppearanceComponent : Component
{
    [DataField]
    public SpriteSpecifier.Rsi[] Sprite = [
        // Carpmosia-start - 5 pipe layers
        new(new("_Carpmosia/Structures/Piping/Atmospherics/pipe.rsi"), "pipeConnector"),
        new(new("_Carpmosia/Structures/Piping/Atmospherics/pipe_alt1.rsi"), "pipeConnector"),
        new(new("_Carpmosia/Structures/Piping/Atmospherics/pipe_alt2.rsi"), "pipeConnector"),
        new(new("_Carpmosia/Structures/Piping/Atmospherics/pipe_alt3.rsi"), "pipeConnector"),
        new(new("_Carpmosia/Structures/Piping/Atmospherics/pipe_alt4.rsi"), "pipeConnector")];
        // Carpmosia-end - 5 pipe layers
}
