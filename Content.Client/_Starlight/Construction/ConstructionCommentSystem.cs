using Content.Client.Construction;
using Content.Client.Paper.UI;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Construction;

/// <summary>
/// Lets players annotate their construction ghosts with a comment that shows up when examining it.
/// </summary>
public sealed class ConstructionCommentSystem : EntitySystem
{
    /// <summary>
    /// Construction recipe used for comment ghosts.
    /// </summary>
    public static readonly ProtoId<ConstructionPrototype> Recipe = "Comment";

    private const int MaxLength = 500;

    private PaperWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConstructionGhostComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
    }

    /// <inheritdoc />
    public override void Shutdown()
    {
        base.Shutdown();

        _window?.Close();
        _window = null;
    }

    /// <summary>
    /// Returns whether a construction ghost is the comment recipe.
    /// </summary>
    public bool IsComment(Entity<ConstructionGhostComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.Prototype?.ID == Recipe.Id;

    /// <summary>
    /// Sets the comment of a ghost, trimmed and clamped to <see cref="MaxLength"/>.
    /// </summary>
    public void SetComment(Entity<ConstructionCommentComponent?> ent, string text)
    {
        Resolve(ent, ref ent.Comp, false);
        text = text.Trim();

        if (text.Length > MaxLength)
            text = text[..MaxLength];

        if (text.Length == 0)
        {
            if (ent.Comp is not null)
                RemComp<ConstructionCommentComponent>(ent.Owner);

            return;
        }

        ent.Comp ??= AddComp<ConstructionCommentComponent>(ent.Owner);
        ent.Comp.Text = text;
    }

    /// <summary>
    /// Gets the comment text attached to a construction ghost, or an empty string when none is attached.
    /// </summary>
    public string GetComment(Entity<ConstructionCommentComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) ? ent.Comp.Text : string.Empty;

    private void OnGetVerbs(Entity<ConstructionGhostComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !IsComment(ent.AsNullable()))
            return;

        var uid = ent.Owner;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("construction-comment-view-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Misc/bureaucracy.rsi"), "paper"),
            ClientExclusive = true,
            Act = () => OpenWindow(uid, PaperComponent.PaperAction.Read),
        });

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("construction-comment-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Misc/bureaucracy.rsi"), "paper_words"),
            ClientExclusive = true,
            Act = () => OpenWindow(uid, PaperComponent.PaperAction.Write),
        });
    }

    /// <summary>
    /// Shows the comment in the paper window, so a note reads and writes like any other paper in the world.
    /// </summary>
    private void OpenWindow(EntityUid uid, PaperComponent.PaperAction mode)
    {
        _window?.Close();

        var window = new PaperWindow();
        _window = window;

        window.MaxInputLength = MaxLength;
        window.Populate(new PaperComponent.PaperBoundUserInterfaceState(
            GetComment(uid),
            new List<StampDisplayInfo>(),
            mode));

        window.OnSaved += text =>
        {
            if (!Deleted(uid))
                SetComment(uid, text);

            window.Close();
        };

        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };

        window.OpenCentered();

        if (mode == PaperComponent.PaperAction.Write)
            window.Input.GrabKeyboardFocus();
    }

    /// <summary>
    /// Called by <see cref="Content.Client.Construction.ConstructionSystem"/>, which owns the
    /// <see cref="ConstructionGhostComponent"/> examine subscription.
    /// </summary>
    public void Examine(Entity<ConstructionGhostComponent?> ent, ExaminedEvent args)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Prototype?.ID != Recipe.Id)
            return;

        var text = GetComment(ent.Owner);
        var message = new FormattedMessage();

        if (text.Length == 0)
        {
            message.AddMarkupOrThrow(Loc.GetString("construction-comment-examine-empty"));
        }
        else
        {
            message.AddMarkupOrThrow(Loc.GetString("construction-comment-examine"));
            message.PushNewline();
            message.AddText(text);
        }

        args.PushMessage(message, 1);
    }
}
