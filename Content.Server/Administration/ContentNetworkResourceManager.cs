using Content.Server.Database;
using Content.Shared.CCVar;
using Robust.Server.Upload;
using Robust.Shared.Configuration;

namespace Content.Server.Administration;

public sealed partial class ContentNetworkResourceManager
{
    [Dependency] private IServerDbManager _serverDb = default!;
    [Dependency] private NetworkResourceManager _netRes = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    [ViewVariables] public bool StoreUploaded { get; set; } = true;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("admin.resources");
        _cfgManager.OnValueChanged(CCVars.ResourceUploadingStoreEnabled, value => StoreUploaded = value, true);
        AutoDelete(_cfgManager.GetCVar(CCVars.ResourceUploadingStoreDeletionDays));
        _netRes.ResourcesUploaded += OnResourcesUploaded;
    }

    private async void OnResourcesUploaded(NetworkResourcesUploadedEvent args)
    {
        if (!StoreUploaded)
            return;

        foreach (var (relative, data) in args.Files)
        {
            try
            {
                await _serverDb.AddUploadedResourceLogAsync(args.Session.UserId, DateTime.Now, relative.ToString(), data);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to persist uploaded resource log for {relative}: {e}");
            }
        }
    }

    private async void AutoDelete(int days)
    {
        if (days > 0)
            await _serverDb.PurgeUploadedResourceLogAsync(days);
    }
}
