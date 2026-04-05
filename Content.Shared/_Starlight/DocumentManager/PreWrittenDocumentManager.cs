using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.DocumentManager;

/// <summary>
///     Preload text documents that are written from text files.
///     This is done because FTL files are really hard to write large documents in.
/// </summary>
public sealed class PreWrittenDocumentManager: IEntityEventSubscriber
{
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    /// <summary>
    ///     Dictionary of file name -> document contents.
    /// </summary>
    private Dictionary<string, string> _nameToDocument = new();

    private const string DocumentsPath = "/Documents/";
    private const string FallbackLocalization = "en-US";

    private const string ValidExtension = "txt";

    public async void Initialize()
    {
        _nameToDocument = await LoadDocuments();
    }

    /// <summary>
    ///     Try to get contents of a document with the given name.
    /// </summary>
    /// <param name="name">The file name (E.g "test_1.txt").</param>
    /// <param name="result">The text stored inside the file.</param>
    /// <returns>If the document was found.</returns>
    public bool TryGetDocumentContents(string name, [NotNullWhen(true)] out string? result)
    {
        _nameToDocument.TryGetValue(name, out result);

        return result != null;
    }

    private Task<Dictionary<string, string>> LoadDocuments()
    {
        return Task.Run(() =>
        {
            var documents = new Dictionary<string, string>();

            LoadFilesFromLoc(_loc.DefaultCulture?.Name, ref documents);
            LoadFilesFromLoc(FallbackLocalization, ref documents);

            return documents;
        });
    }

    private void LoadFilesFromLoc(string? locName, ref Dictionary<string, string> documents)
    {
        if (locName == null)
            return;

        var path = new ResPath(DocumentsPath) / locName;
        foreach (var file in _resource.ContentFindFiles(path))
        {
            if (file.Extension != ValidExtension)
                continue;

            var text = _resource.ContentFileReadAllText(file);

            // no dupes!
            DebugTools.Assert(!_nameToDocument.ContainsKey(file.Filename));

            documents.TryAdd(file.Filename, text);
        }
    }
}
