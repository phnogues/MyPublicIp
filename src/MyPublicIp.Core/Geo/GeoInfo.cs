using System.Globalization;

namespace MyPublicIp.Core.Geo;

/// <summary>Approximate location and provider of a public IP address.</summary>
public sealed record GeoInfo(string CountryCode, string? City, string? Region, string? Provider)
{
    /// <summary>
    /// Country name in English, like the city and region names returned by the geolocation services
    /// (.NET gives no reliable localized name: DisplayName can come back in the country's own language).
    /// </summary>
    public string CountryName
    {
        get
        {
            try
            {
                return new RegionInfo(CountryCode).EnglishName;
            }
            catch (ArgumentException)
            {
                return CountryCode;
            }
        }
    }
}
