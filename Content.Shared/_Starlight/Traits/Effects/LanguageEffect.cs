using Content.Shared._Starlight.Language.Systems;

namespace Content.Shared._Starlight.Traits.Effects;

/// <summary>
/// Effect that add/replace a background to the player entity.
/// </summary>
public sealed partial class LanguageEffect : BaseTraitEffect
{
    /// <summary>
    /// The list of all Spoken Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<string>? LanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<string>? LanguagesUnderstood { get; private set; } = default!;

    /// <summary>
    ///     The list of all Spoken Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<string>? RemoveLanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<string>? RemoveLanguagesUnderstood { get; private set; } = default!;

    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.EntitySysManager.TryGetEntitySystem(out SharedLanguageSystem? language))
            return;

        if (RemoveLanguagesSpoken is not null)
            foreach (var lang in RemoveLanguagesSpoken)
                language.RemoveLanguage(ctx.Player, lang, true, false);

        if (RemoveLanguagesUnderstood is not null)
            foreach (var lang in RemoveLanguagesUnderstood)
                language.RemoveLanguage(ctx.Player, lang, false, true);

        if (LanguagesSpoken is not null)
            foreach (var lang in LanguagesSpoken)
                language.AddLanguage(ctx.Player, lang, true, false);

        if (LanguagesUnderstood is not null)
            foreach (var lang in LanguagesUnderstood)
                language.AddLanguage(ctx.Player, lang, false, true);
    }
}
