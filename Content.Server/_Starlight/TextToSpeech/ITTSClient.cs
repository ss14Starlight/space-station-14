using System.Threading;
namespace Content.Server._Starlight.TextToSpeech;

public interface ITTSClient
{
    IAsyncEnumerable<byte[]> GenerateTTS(string text, int voice, TTSEffect effect = TTSEffect.None, CancellationToken cancellationToken = default);
    void Initialize();
}