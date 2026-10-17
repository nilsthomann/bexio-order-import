using BexioOrderImport.Wpf.Services;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private async Task CheckForUpdatesAsync()
    {
        UpdateInfo? info;
        try
        {
            info = await _updateService.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            // A failed update check must not disturb the app; it only leaves a note in the log
            InvokeOnUi(() => AppendLog($"⚠️ Update check failed: {ex.Message}"));
            return;
        }

        if (info != null)
        {
            InvokeOnUi(() =>
            {
                _updateDownloadUrl = info.DownloadUrl;
                UpdateVersion = info.LatestVersion;
                IsUpdateAvailable = true;
                InstallUpdateCommand.RaiseCanExecuteChanged();
            });
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl)) return;

        IsDownloadingUpdate = true;
        InstallUpdateCommand.RaiseCanExecuteChanged();
        UpdateStatusText = string.Format(Resources.Translations.Update_Downloading, 0);

        try
        {
            await _updateService.DownloadAndInstallUpdateAsync(_updateDownloadUrl, progress =>
            {
                InvokeOnUi(() =>
                {
                    UpdateProgress = progress;
                    UpdateStatusText = string.Format(Resources.Translations.Update_Downloading, progress);
                });
            });
        }
        catch (Exception ex)
        {
            InvokeOnUi(() =>
            {
                IsDownloadingUpdate = false;
                InstallUpdateCommand.RaiseCanExecuteChanged();
                UpdateStatusText = string.Format(Resources.Translations.Update_Error, ex.Message);
            });
        }
    }
}
