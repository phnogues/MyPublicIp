using MyPublicIp.Core.Abstractions;
using Windows.ApplicationModel;
using Windows.System;

namespace MyPublicIp.Platform;

/// <summary>
/// "Start with Windows" through the MSIX <c>windows.startupTask</c> extension declared in
/// Package.appxmanifest. The user can also manage it in Settings &gt; Apps &gt; Startup.
/// </summary>
internal sealed class PackagedStartupManager : IStartupManager
{
    private const string TaskId = "MyPublicIpStartup";

    public async Task<StartupState> GetStateAsync()
    {
        StartupTask? task = await GetTaskAsync();
        return task is null ? StartupState.Unsupported : ToStartupState(task.State);
    }

    public async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        StartupTask? task = await GetTaskAsync();
        if (task is null)
        {
            return StartupState.Unsupported;
        }

        switch (task.State)
        {
            case StartupTaskState.Enabled when !enabled:
                task.Disable();
                break;

            case StartupTaskState.Disabled when enabled:
                return ToStartupState(await task.RequestEnableAsync());

            case StartupTaskState.DisabledByUser when enabled:
                // Only the user can turn it back on, from Windows Settings
                await Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps"));
                break;
        }

        return ToStartupState(task.State);
    }

    private static async Task<StartupTask?> GetTaskAsync()
    {
        try
        {
            return await StartupTask.GetAsync(TaskId);
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run)
            return null;
        }
        catch (ArgumentException)
        {
            // Task not declared in the manifest
            return null;
        }
    }

    private static StartupState ToStartupState(StartupTaskState state) => state switch
    {
        StartupTaskState.Disabled => StartupState.Disabled,
        StartupTaskState.DisabledByUser => StartupState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupState.DisabledByPolicy,
        StartupTaskState.Enabled => StartupState.Enabled,
        StartupTaskState.EnabledByPolicy => StartupState.EnabledByPolicy,
        _ => StartupState.Unsupported,
    };
}
