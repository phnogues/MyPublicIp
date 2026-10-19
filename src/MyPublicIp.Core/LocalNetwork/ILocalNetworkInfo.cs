namespace MyPublicIp.Core.LocalNetwork;

public interface ILocalNetworkInfo
{
    /// <returns>The adapter used to reach the internet (outside of any VPN), or null when offline.</returns>
    NetworkDetails? GetPrimaryNetwork();

    VpnStatus GetVpnStatus();
}
