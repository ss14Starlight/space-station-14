using Content.Shared._Starlight.DocumentManager;
using Content.Shared.Paper;

namespace Content.Shared._Starlight.Paper;

public sealed class TextFilePaperContentSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PreWrittenDocumentManager _documentManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TextFilePaperContentComponent, MapInitEvent>(OnTextFilePaperContentComponentInit);
    }
    
    private void OnTextFilePaperContentComponentInit(Entity<TextFilePaperContentComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PaperComponent>(ent, out var paperComp))
            return;

        if (!_documentManager.TryGetDocumentContents(ent.Comp.FileName, out var contents))
            return;

        _paper.SetContent((ent, paperComp), contents);

        RemCompDeferred(ent, ent.Comp);
    }
}
