using System.IO;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Robust.Shared.Maths;
using Robust.Shared.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Replaces RSI image loading with dummy in-memory images during integration tests.
///     Avoids reading thousands of PNG files from disk — the main bottleneck of client initialization.
///     Pixel data is irrelevant for integration tests since nothing is rendered.
/// </summary>
internal static class RsiLoadingPatch
{
    private static Hook? _hook;

    // Delegate matching the signature of RsiLoading.LoadImages
    private delegate Image<Rgba32>[] LoadImagesDelegate(
        object metadata,
        object configuration,
        Func<string, Stream> openStream);

    internal static void Apply()
    {
        // Find the internal type by name via a public type from the same assembly.
        var rsiLoadingType = typeof(RSILoadException).Assembly
            .GetType("Robust.Shared.Resources.RsiLoading");

        if (rsiLoadingType == null)
        {
            TestContext.Error.WriteLine("[RsiLoadingPatch] Could not find RsiLoading type — patch skipped.");
            return;
        }

        var original = rsiLoadingType.GetMethod(
            "LoadImages",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        if (original == null)
        {
            TestContext.Error.WriteLine("[RsiLoadingPatch] Could not find LoadImages method — patch skipped.");
            return;
        }

        _hook = new Hook(original, LoadImagesReplacement);
    }

    internal static void Unpatch()
    {
        _hook?.Dispose();
        _hook = null;
    }

    private static Image<Rgba32>[] LoadImagesReplacement(
        LoadImagesDelegate _,
        object metadata,
        object configuration,
        Func<string, Stream> openStream)
    {
        var metaType = metadata.GetType();
        var frameSize = (Vector2i) metaType.GetField("Size")!.GetValue(metadata)!;
        var states = (Array) metaType.GetField("States")!.GetValue(metadata)!;

        var images = new Image<Rgba32>[states.Length];
        for (var i = 0; i < states.Length; i++)
        {
            var state = states.GetValue(i)!;
            var delays = (float[][]) state.GetType().GetField("Delays")!.GetValue(state)!;
            var totalFrames = delays.Sum(d => d.Length);
            // Image must have correct dimensions so GenerateAtlas can blit frames correctly.
            images[i] = new Image<Rgba32>(frameSize.X, Math.Max(1, totalFrames) * frameSize.Y);
        }

        return images;
    }
}
