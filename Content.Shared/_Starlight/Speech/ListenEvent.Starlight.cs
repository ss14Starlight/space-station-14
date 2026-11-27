// ReSharper disable CheckNamespace
namespace Content.Shared.Speech;

public sealed partial class ListenEvent
{
    /// <summary>
    /// Text for TTS, which accents may set apart from <see cref="Message"/>.
    /// Null means TTS should read <see cref="Message"/>.
    /// </summary>
    public string? Tts { get; init; }
}
