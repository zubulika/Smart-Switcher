using System.Reflection;
using Velopack;
using Velopack.Sources;
using SmartSwitcher.Logging;

namespace SmartSwitcher.Core;

public enum UpdateState
{
    Idle,
    Checking,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    UpToDate,
    Failed
}

public class UpdateService
{
    private const string RepoUrl = "https://github.com/zubulika/Smart-Switcher";
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _downloadedUpdate;

    public UpdateState State { get; private set; } = UpdateState.Idle;
    public string? LatestVersion { get; private set; }
    public string? CurrentVersion { get; }

    public event Action<UpdateState, string?>? StateChanged;

    public UpdateService()
    {
        CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var source = new GithubSource(RepoUrl, null, false);
        _updateManager = new UpdateManager(source);
    }

    public async Task<bool> CheckAndDownloadUpdatesAsync(bool silent = true)
    {
        if (State == UpdateState.Checking || State == UpdateState.Downloading)
            return false;

        try
        {
            SetState(UpdateState.Checking, "Checking for updates...");
            AppLogger.Info("[Update] Checking for updates from GitHub Releases...");

            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                SetState(UpdateState.UpToDate, "Smart Switcher is up to date.");
                AppLogger.Info("[Update] App is up to date.");
                return false;
            }

            LatestVersion = updateInfo.TargetFullRelease.Version.ToString();
            SetState(UpdateState.UpdateAvailable, $"New version {LatestVersion} is available.");
            AppLogger.Info($"[Update] Found newer release: {LatestVersion}. Downloading package...");

            SetState(UpdateState.Downloading, $"Downloading update {LatestVersion}...");
            await _updateManager.DownloadUpdatesAsync(updateInfo);

            _downloadedUpdate = updateInfo;
            SetState(UpdateState.ReadyToInstall, $"Version {LatestVersion} ready to install.");
            AppLogger.Info($"[Update] Update {LatestVersion} downloaded and ready to apply.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[Update] Auto-update check/download failed: {ex.Message}");
            SetState(UpdateState.Failed, silent ? null : $"Update error: {ex.Message}");
            return false;
        }
    }

    public void RestartAndApplyUpdate()
    {
        if (_downloadedUpdate != null)
        {
            AppLogger.Info($"[Update] Applying update {_downloadedUpdate.TargetFullRelease.Version} and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_downloadedUpdate);
        }
    }

    private void SetState(UpdateState newState, string? message)
    {
        State = newState;
        StateChanged?.Invoke(newState, message);
    }
}
