using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

/// <summary>
/// Keeps config var values in fields for code that reads them often.
/// </summary>
/// <remarks>
/// Entity systems should use <c>Subs.CVar</c>, which already unsubscribes on shutdown.
/// This class is for overlays, UI controls and other objects that are not entity systems.
/// </remarks>
public sealed class CVarSubscriptions(IConfigurationManager cfg) : IDisposable
{
    private readonly List<Action> _unsubscribe = [];

    /// <summary>
    /// Calls <paramref name="onChanged"/> with every new value of <paramref name="cvar"/>.
    /// It is also called once right away with the current value, unless <paramref name="invokeImmediately"/> is false.
    /// </summary>
    public CVarSubscriptions Track<T>(CVarDef<T> cvar, Action<T> onChanged, bool invokeImmediately = true)
        where T : notnull
    {
        cfg.OnValueChanged(cvar, onChanged, invokeImmediately);
        _unsubscribe.Add(() => cfg.UnsubValueChanged(cvar, onChanged));
        return this;
    }

    public void Dispose()
    {
        foreach (var unsubscribe in _unsubscribe)
            unsubscribe();

        _unsubscribe.Clear();
    }
}
