using System.Diagnostics.CodeAnalysis;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.EventBus;

public interface INullLinkEventBusManager
{
    void Initialize();
    void Shutdown();
    bool TryDequeue([MaybeNullWhen(false)] out BaseEvent result);

    event Action<AdminNote>? NoteAdded;

    event Action<AdminNote>? NoteChanged;

    event Action<AdminNote>? NoteRemoved;
}