using System.Net.NetworkInformation;

namespace MyPublicIp.Core.PublicIp;

/// <summary><see cref="INetworkMonitor"/> backed by the operating system notifications.</summary>
public sealed class SystemNetworkMonitor : INetworkMonitor, IDisposable
{
    public SystemNetworkMonitor()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    public event EventHandler? NetworkChanged;

    public bool IsNetworkAvailable => NetworkInterface.GetIsNetworkAvailable();

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => NetworkChanged?.Invoke(this, EventArgs.Empty);
}
