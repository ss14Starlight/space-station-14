using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using MonoMod.RuntimeDetour;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Patches <see cref="SerializationManager.Initialize"/> so that:
///     <list type="bullet">
///         <item>The first call with a given assembly fingerprint runs normally and caches the
///         <b>entire first <see cref="SerializationManager"/> instance</b> (the "donor").</item>
///         <item>All subsequent calls with the same fingerprint copy the pre-built metadata
///         (DataDefinitions, flags/constants, serializer provider, copyByRef) from the donor into
///         the new instance, <b>then re-point the donor's <c>DependencyCollection</c> and
///         <c>_reflectionManager</c> backing fields</b> at the current IoC container so that
///         compiled expression-tree delegates (which capture the donor via
///         <c>Expression.Constant(manager)</c>) resolve services from the correct container.</item>
///     </list>
/// </summary>
internal static class SerializationManagerPatch
{
    private static Hook s_initHook;
    private static Hook s_shutdownHook;
    private static int s_applied;

    private static readonly FieldInfo _initializedField =
        typeof(SerializationManager).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _initializingField =
        typeof(SerializationManager).GetField("_initializing", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _dataDefinitionsField =
        typeof(SerializationManager).GetField("_dataDefinitions", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _copyByRefField =
        typeof(SerializationManager).GetField("_copyByRefRegistrations", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _serializerProviderField =
        typeof(SerializationManager).GetField("_regularSerializerProvider", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _flagsMappingField =
        typeof(SerializationManager).GetField("_flagsMapping", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _highestFlagBitField =
        typeof(SerializationManager).GetField("_highestFlagBit", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _constantsMappingField =
        typeof(SerializationManager).GetField("_constantsMapping", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly PropertyInfo _reflectionManagerProp =
        typeof(SerializationManager).GetProperty(
            nameof(SerializationManager.ReflectionManager),
            BindingFlags.Instance | BindingFlags.Public)!;

    private static readonly FieldInfo _reflectionManagerBackingField =
        typeof(SerializationManager).GetField("_reflectionManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _dependencyCollectionBackingField =
        typeof(SerializationManager).GetField("<DependencyCollection>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class SerializationCache
    {
        public required SerializationManager Donor;

        public required object DataDefinitions;
        public required object CopyByRefRegistrations;
        public required object RegularSerializerProvider;
        public required object FlagsMapping;
        public required object HighestFlagBit;
        public required object ConstantsMapping;
    }

    private static readonly ConcurrentDictionary<string, SerializationCache> _cache = new();

    private static readonly ConcurrentDictionary<SerializationManager, byte> _managedInstances = new();

    private delegate void OrigDelegate(SerializationManager self);

    internal static void Apply()
    {
        if (Interlocked.Exchange(ref s_applied, 1) != 0)
            return;

        var initMethod = typeof(SerializationManager).GetMethod(
            nameof(SerializationManager.Initialize),
            BindingFlags.Instance | BindingFlags.Public);

        var shutdownMethod = typeof(SerializationManager).GetMethod(
            nameof(SerializationManager.Shutdown),
            BindingFlags.Instance | BindingFlags.Public);

        if (initMethod == null || shutdownMethod == null)
        {
            TestContext.Error.WriteLine("[SerializationManagerPatch] Could not find Initialize/Shutdown — patch skipped.");
            return;
        }

        s_initHook = new Hook(initMethod, InitializeHook);
        s_shutdownHook = new Hook(shutdownMethod, ShutdownHook);
    }

    internal static void Unpatch()
    {
        s_initHook?.Dispose();
        s_initHook = null;
        s_shutdownHook?.Dispose();
        s_shutdownHook = null;
        Interlocked.Exchange(ref s_applied, 0);
        _cache.Clear();
        _managedInstances.Clear();
    }

    private static string GetFingerprint(SerializationManager self)
    {
        var rm = (IReflectionManager)_reflectionManagerProp.GetValue(self)!;
        return string.Join("|", rm.Assemblies
            .Select(a => a.GetName().Name!)
            .OrderBy(n => n, StringComparer.Ordinal));
    }

    private static void InitializeHook(OrigDelegate orig, SerializationManager self)
    {
        if ((bool)_initializedField.GetValue(self)!)
            return;

        var key = GetFingerprint(self);

        if (_cache.TryGetValue(key, out var cached))
        {
            // Point this instance's fields at the shared (cached) dictionaries.
            _dataDefinitionsField.SetValue(self, cached.DataDefinitions);
            _copyByRefField.SetValue(self, cached.CopyByRefRegistrations);
            _serializerProviderField.SetValue(self, cached.RegularSerializerProvider);
            _flagsMappingField.SetValue(self, cached.FlagsMapping);
            _highestFlagBitField.SetValue(self, cached.HighestFlagBit);
            _constantsMappingField.SetValue(self, cached.ConstantsMapping);

            _dependencyCollectionBackingField.SetValue(cached.Donor, self.DependencyCollection);
            _reflectionManagerBackingField.SetValue(cached.Donor, _reflectionManagerProp.GetValue(self));

            _initializingField.SetValue(self, false);
            _initializedField.SetValue(self, true);

            _managedInstances[self] = 0;
            return;
        }

        orig(self);

        _cache.TryAdd(key, new SerializationCache
        {
            Donor = self,
            DataDefinitions = _dataDefinitionsField.GetValue(self)!,
            CopyByRefRegistrations = _copyByRefField.GetValue(self)!,
            RegularSerializerProvider = _serializerProviderField.GetValue(self)!,
            FlagsMapping = _flagsMappingField.GetValue(self)!,
            HighestFlagBit = _highestFlagBitField.GetValue(self)!,
            ConstantsMapping = _constantsMappingField.GetValue(self)!,
        });

        _managedInstances[self] = 0;
    }

    private static void ShutdownHook(OrigDelegate orig, SerializationManager self)
    {
        if (_managedInstances.TryRemove(self, out _))
        {
            _initializedField.SetValue(self, false);
            _initializingField.SetValue(self, false);
            return;
        }
        orig(self);
    }
}
