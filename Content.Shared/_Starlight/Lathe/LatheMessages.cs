
// ReSharper disable CheckNamespace
namespace Content.Shared.Lathe;

public sealed partial class LatheUpdateState
{
    /// <summary>
    /// When the current item started, in server time, or null when nothing is being made.
    /// </summary>
    public TimeSpan? ProductionStart;

    /// <summary>
    /// How long the current item takes in total.
    /// </summary>
    public TimeSpan ProductionLength;
}
