using CommunityToolkit.Mvvm.ComponentModel;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.Geo;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.LocalNetwork;

namespace MyPublicIp.Core.ViewModels;

/// <summary>What the public address says about the connection: location, provider, VPN, latency.</summary>
public sealed partial class ConnectionViewModel : ObservableObject, IDisposable
{
    private readonly IGeoLocator _geo;
    private readonly LatencyMonitor _latency;
    private readonly ILocalizer _localizer;
    private readonly UiDispatcher _dispatcher = new();
    private CancellationTokenSource? _lookup;
    private string? _locatedAddress;

    public ConnectionViewModel(IGeoLocator geo, LatencyMonitor latency, ILocalizer localizer)
    {
        _geo = geo;
        _latency = latency;
        _localizer = localizer;
        _latency.Changed += OnLatencyChanged;
        UpdateVpn(VpnStatus.Off);
        UpdateLatency();
    }

    [ObservableProperty]
    public partial bool HasLocation { get; private set; }

    [ObservableProperty]
    public partial string CountryCode { get; private set; } = string.Empty;

    /// <summary>"Paris, Île-de-France, France"</summary>
    [ObservableProperty]
    public partial string LocationText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ProviderText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVpnActive { get; private set; }

    /// <summary>"VPN on · NordLynx" or "VPN off"</summary>
    [ObservableProperty]
    public partial string VpnText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial ConnectionQuality Quality { get; private set; }

    /// <summary>"18 ms · Excellent"</summary>
    [ObservableProperty]
    public partial string LatencyText { get; private set; } = string.Empty;

    /// <summary>Last measurements in milliseconds, oldest first; null when the server did not answer.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<double?> LatencyHistory { get; private set; } = [];

    public VpnStatus Vpn { get; private set; } = VpnStatus.Off;

    public GeoInfo? Location { get; private set; }

    /// <summary>Measures the latency every few seconds while it is on screen, once a minute otherwise.</summary>
    public void SetLive(bool live) => _latency.SetFastMode(live);

    public void UpdateVpn(VpnStatus vpn)
    {
        Vpn = vpn;
        IsVpnActive = vpn.IsActive;
        VpnText = vpn.IsActive
            ? _localizer.Format("VpnOn", vpn.Name ?? "VPN")
            : _localizer.Get("VpnOff");
    }

    /// <summary>Looks up the location of a new public address; the previous lookup is abandoned.</summary>
    public async Task<GeoInfo?> LocateAsync(string? address)
    {
        if (address == _locatedAddress)
        {
            return Location;
        }

        _locatedAddress = address;
        if (_lookup is not null)
        {
            await _lookup.CancelAsync();
        }

        ShowLocation(null);
        if (address is null)
        {
            return null;
        }

        using var lookup = new CancellationTokenSource();
        _lookup = lookup;
        try
        {
            GeoInfo? info = await _geo.LocateAsync(address, lookup.Token);
            if (address == _locatedAddress)
            {
                ShowLocation(info);
            }

            return info;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            if (_lookup == lookup)
            {
                _lookup = null;
            }
        }
    }

    public void Dispose()
    {
        _latency.Changed -= OnLatencyChanged;
        _lookup?.Cancel();
    }

    private void ShowLocation(GeoInfo? info)
    {
        Location = info;
        HasLocation = info is not null;
        CountryCode = info?.CountryCode ?? string.Empty;
        LocationText = info is null
            ? string.Empty
            : string.Join(", ", new[] { info.City, info.Region, info.CountryName }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
        ProviderText = info?.Provider ?? string.Empty;
    }

    private void OnLatencyChanged(object? sender, EventArgs e) => _dispatcher.Post(UpdateLatency);

    private void UpdateLatency()
    {
        IReadOnlyList<TimeSpan?> samples = _latency.Samples;
        LatencyHistory = [.. samples.Select(s => s?.TotalMilliseconds)];
        if (samples.Count == 0)
        {
            Quality = ConnectionQuality.Unknown;
            LatencyText = _localizer.Get("LatencyMeasuring");
            return;
        }

        TimeSpan? latest = samples[^1];
        Quality = ConnectionQualityRating.From(latest);
        string quality = _localizer.Get($"Quality{Quality}");
        LatencyText = latest is { } value
            ? _localizer.Format("LatencyValue", Math.Round(value.TotalMilliseconds), quality)
            : quality;
    }
}
