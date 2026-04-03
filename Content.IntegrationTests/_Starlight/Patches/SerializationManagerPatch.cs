using System.Reflection;
using System.Threading;
using MonoMod.RuntimeDetour;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Patches <see cref="SerializationManager.Initialize"/> to be idempotent:
///     a second call on an already-initialized instance is silently ignored instead of throwing.
///     This lets integration tests share a single pre-warmed <see cref="SerializationManager"/>
///     instance across all server/client pairs without touching the engine.
/// </summary>
internal static class SerializationManagerPatch
{
    private static Hook? _hook;
    private static int _applied;

    private static readonly FieldInfo InitializedField =
        typeof(SerializationManager).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private delegate void InitializeDelegate(SerializationManager self);

    internal static void Apply()
    {
        if (Interlocked.Exchange(ref _applied, 1) != 0)
            return;

        var method = typeof(SerializationManager).GetMethod(
            nameof(SerializationManager.Initialize),
            BindingFlags.Instance | BindingFlags.Public);

        if (method == null)
        {
            TestContext.Error.WriteLine("[SerializationManagerPatch] Could not find Initialize method — patch skipped.");
            return;
        }

        _hook = new Hook(method, InitializeHook);
    }

    internal static void Unpatch()
    {
        _hook?.Dispose();
        _hook = null;
    }

    private static void InitializeHook(InitializeDelegate orig, SerializationManager self)
    {
        if ((bool) InitializedField.GetValue(self)!)
            return;

        orig(self);
    }
}
