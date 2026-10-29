// ReSharper disable CheckNamespace
namespace Content.Client.UserInterface.Controls;

public partial class StripeBack
{
    private void GetVerticalEdgeInsets(float availableHeight, float scale, out float topInset, out float bottomInset)
    {
        var padSize = HasMargins ? MathF.Max(0f, PadSize) : 0f;
        var edgeInset = (padSize + MathF.Max(0f, EdgeSize)) * scale;
        topInset = HasTopEdge ? edgeInset : 0f;
        bottomInset = HasBottomEdge ? edgeInset : 0f;

        var totalInset = topInset + bottomInset;
        var height = MathF.Max(0f, availableHeight);
        if (totalInset <= height || totalInset <= 0f)
            return;

        var fit = height / totalInset;
        topInset = MathF.Min(topInset * fit, height); // Starlight
        bottomInset = MathF.Min(bottomInset * fit, height - topInset); // Starlight
    }
}
