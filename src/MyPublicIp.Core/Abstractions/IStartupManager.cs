namespace MyPublicIp.Core.Abstractions;

/// <summary>"Start with Windows" setting.</summary>
public interface IStartupManager
{
    Task<StartupState> GetStateAsync();

    /// <summary>Requests a change and returns the resulting state, which may differ from the request.</summary>
    Task<StartupState> SetEnabledAsync(bool enabled);
}

public enum StartupState
{
    /// <summary>Not available, for instance when the app runs without package identity.</summary>
    Unsupported,
    Disabled,
    /// <summary>Turned off by the user in Windows Settings: only the user can turn it back on there.</summary>
    DisabledByUser,
    DisabledByPolicy,
    Enabled,
    EnabledByPolicy,
}

public static class StartupStateExtensions
{
    public static bool IsOn(this StartupState state) =>
        state is StartupState.Enabled or StartupState.EnabledByPolicy;

    public static bool CanChange(this StartupState state) =>
        state is StartupState.Enabled or StartupState.Disabled or StartupState.DisabledByUser;
}
