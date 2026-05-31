using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Robust.Shared.Maths;
using Robust.Shared.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Replaces RSI image loading with dummy in-memory images during integration tests.
///     Avoids reading thousands of PNG files from disk — the main bottleneck of client initialization.
///     Pixel data is irrelevant for integration tests since nothing is rendered.
/// </summary>
internal static class RsiLoadingPatch
{
    private static Hook _hook;

    private static readonly HashSet<string> _realImageRsiPaths = [
        "Effects/clicktest.rsi",
    ];

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
        LoadImagesDelegate orig,
        object metadata,
        object configuration,
        Func<string, Stream> openStream)
    {
        var rsiPath = TryGetRsiPath(openStream);
        if (rsiPath != null && _realImageRsiPaths.Contains(rsiPath))
            return orig(metadata, configuration, openStream);

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
            var img = new Image<Rgba32>(frameSize.X, Math.Max(1, totalFrames) * frameSize.Y);
            img.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.White));
            images[i] = img;
        }

        return images;
    }

    /// <summary>
    ///     Extracts the RSI path from the <paramref name="openStream"/> closure.
    ///     The lambda captures a <c>LoadStepData</c> instance whose <c>Path</c> field
    ///     is a <c>ResPath</c> struct with a <c>CanonPath</c> string, e.g.
    ///     <c>/Textures/Effects/clicktest.rsi</c>. Returns the path with the
    ///     leading <c>/Textures/</c> prefix stripped, or <c>null</c> if not found.
    /// </summary>
    private static string TryGetRsiPath(Func<string, Stream> openStream)
    {
        var target = openStream.Target;
        if (target == null)
            return null;

        foreach (var closureField in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var captured = closureField.GetValue(target);
            if (captured == null)
                continue;

            var pathField = captured.GetType().GetField("Path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pathField == null)
                continue;

            var pathVal = pathField.GetValue(captured);
            if (pathVal == null)
                continue;

            var canonField = pathVal.GetType().GetField("CanonPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (canonField == null)
                continue;

            if (canonField.GetValue(pathVal) is not string canon)
                continue;

            const string Prefix = "/Textures/";
            return canon.StartsWith(Prefix, StringComparison.Ordinal) ? canon[Prefix.Length..] : canon;
        }

        return null;
    }
}
