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
    private static Hook s_hook;
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

    // Backing fields for DI properties that the compiled delegates reach through.
    private static readonly FieldInfo _reflectionManagerBackingField =
        typeof(SerializationManager).GetField("_reflectionManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo _dependencyCollectionBackingField =
        typeof(SerializationManager).GetField("<DependencyCollection>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;

    // Cached donor instance keyed by assembly fingerprint

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

    private delegate void InitializeDelegate(SerializationManager self);

    internal static void Apply()
    {
        if (Interlocked.Exchange(ref s_applied, 1) != 0)
            return;

        var method = typeof(SerializationManager).GetMethod(
            nameof(SerializationManager.Initialize),
            BindingFlags.Instance | BindingFlags.Public);

        if (method == null)
        {
            TestContext.Error.WriteLine("[SerializationManagerPatch] Could not find Initialize method — patch skipped.");
            return;
        }

        s_hook = new Hook(method, InitializeHook);
    }

    internal static void Unpatch()
    {
        s_hook?.Dispose();
        s_hook = null;
        Interlocked.Exchange(ref s_applied, 0);
        _cache.Clear();
    }

    private static string GetFingerprint(SerializationManager self)
    {
        var rm = (IReflectionManager)_reflectionManagerProp.GetValue(self)!;
        return string.Join("|", rm.Assemblies
            .Select(a => a.GetName().Name!)
            .OrderBy(n => n, StringComparer.Ordinal));
    }

    private static void InitializeHook(InitializeDelegate orig, SerializationManager self)
    {
        if ((bool)_initializedField.GetValue(self)!)
            return;

        var key = GetFingerprint(self);

        if (_cache.TryGetValue(key, out var cached))
        {
            // Copy all data structures into the new instance.
            _dataDefinitionsField.SetValue(self, cached.DataDefinitions);
            _copyByRefField.SetValue(self, cached.CopyByRefRegistrations);
            _serializerProviderField.SetValue(self, cached.RegularSerializerProvider);
            _flagsMappingField.SetValue(self, cached.FlagsMapping);
            _highestFlagBitField.SetValue(self, cached.HighestFlagBit);
            _constantsMappingField.SetValue(self, cached.ConstantsMapping);

            // The compiled delegates inside DataDefinitions call methods on the donor
            // (captured via Expression.Constant). Those methods use this.DependencyCollection
            // and this._reflectionManager. Repoint the donor's backing fields to the
            // current IoC container so the delegates resolve services correctly.
            var currentDepCollection = self.DependencyCollection;
            var currentReflectionManager = _reflectionManagerProp.GetValue(self);

            _dependencyCollectionBackingField.SetValue(cached.Donor, currentDepCollection);
            _reflectionManagerBackingField.SetValue(cached.Donor, currentReflectionManager);

            _initializingField.SetValue(self, false);
            _initializedField.SetValue(self, true);
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
    }
}
