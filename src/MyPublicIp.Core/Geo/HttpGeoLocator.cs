using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MyPublicIp.Core.Geo;

/// <summary>
/// Asks public geolocation services (ipinfo.io, then ipapi.co) where an address is registered.
/// Results are cached per address: a lookup only happens when the public IP changes.
/// </summary>
public sealed partial class HttpGeoLocator : IGeoLocator
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, GeoInfo> _cache = [];
    private readonly Lock _gate = new();

    public HttpGeoLocator(HttpClient http)
    {
        _http = http;
    }

    public async Task<GeoInfo?> LocateAsync(string address, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(address, out GeoInfo? cached))
            {
                return cached;
            }
        }

        string escaped = Uri.EscapeDataString(address);
        GeoInfo? info = await TryAsync(
                new Uri($"https://ipinfo.io/{escaped}/json"),
                GeoJsonContext.Default.IpInfoResponse,
                r => r.Bogon ? null : ToGeoInfo(r.Country, r.City, r.Region, r.Org),
                cancellationToken).ConfigureAwait(false)
            ?? await TryAsync(
                new Uri($"https://ipapi.co/{escaped}/json/"),
                GeoJsonContext.Default.IpApiResponse,
                r => r.Error ? null : ToGeoInfo(r.CountryCode, r.City, r.Region, r.Org),
                cancellationToken).ConfigureAwait(false);

        if (info is not null)
        {
            lock (_gate)
            {
                _cache[address] = info;
            }
        }

        return info;
    }

    /// <summary>"AS3215 Orange S.A." becomes "Orange S.A.".</summary>
    internal static string? CleanProvider(string? organization)
    {
        if (string.IsNullOrWhiteSpace(organization))
        {
            return null;
        }

        return AsNumberPrefix().Replace(organization, string.Empty).Trim();
    }

    private static GeoInfo? ToGeoInfo(string? country, string? city, string? region, string? organization) =>
        string.IsNullOrWhiteSpace(country) || country.Length != 2
            ? null
            : new GeoInfo(country.ToUpperInvariant(), NullIfEmpty(city), NullIfEmpty(region), CleanProvider(organization));

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<GeoInfo?> TryAsync<TResponse>(
        Uri uri,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> typeInfo,
        Func<TResponse, GeoInfo?> convert,
        CancellationToken cancellationToken)
    {
        try
        {
            TResponse? response = await _http.GetFromJsonAsync(uri, typeInfo, cancellationToken).ConfigureAwait(false);
            return response is null ? null : convert(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Rate limited, down or unexpected answer: try the next service
            return null;
        }
    }

    [GeneratedRegex(@"^AS\d+\s+", RegexOptions.CultureInvariant)]
    private static partial Regex AsNumberPrefix();
}

internal sealed record IpInfoResponse(
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("region")] string? Region,
    [property: JsonPropertyName("org")] string? Org,
    [property: JsonPropertyName("bogon")] bool Bogon);

internal sealed record IpApiResponse(
    [property: JsonPropertyName("country_code")] string? CountryCode,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("region")] string? Region,
    [property: JsonPropertyName("org")] string? Org,
    [property: JsonPropertyName("error")] bool Error);

/// <summary>Source-generated serializers: no reflection, safe with the trimmed Store build.</summary>
[JsonSerializable(typeof(IpInfoResponse))]
[JsonSerializable(typeof(IpApiResponse))]
internal sealed partial class GeoJsonContext : JsonSerializerContext;
