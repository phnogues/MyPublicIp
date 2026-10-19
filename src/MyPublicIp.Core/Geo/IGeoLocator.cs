namespace MyPublicIp.Core.Geo;

public interface IGeoLocator
{
    /// <returns>Where the address is registered, or null when no service could tell.</returns>
    Task<GeoInfo?> LocateAsync(string address, CancellationToken cancellationToken);
}
