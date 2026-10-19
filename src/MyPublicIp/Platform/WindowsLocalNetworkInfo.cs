using MyPublicIp.Core.LocalNetwork;
using Windows.Networking.Connectivity;

namespace MyPublicIp.Platform;

/// <summary>
/// Adds what only Windows knows to the adapter details: the network profile name (the Wi-Fi name
/// for wireless networks) and the signal strength. Neither needs the location permission.
/// </summary>
internal sealed class WindowsLocalNetworkInfo : SystemLocalNetworkInfo
{
    public override NetworkDetails? GetPrimaryNetwork()
    {
        NetworkDetails? details = base.GetPrimaryNetwork();
        if (details is null)
        {
            return null;
        }

        try
        {
            ConnectionProfile? profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null)
            {
                return details;
            }

            return details with
            {
                NetworkName = string.IsNullOrWhiteSpace(profile.ProfileName) ? null : profile.ProfileName,
                SignalBars = profile.IsWlanConnectionProfile || profile.IsWwanConnectionProfile ? profile.GetSignalBars() : null,
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return details;
        }
    }
}
