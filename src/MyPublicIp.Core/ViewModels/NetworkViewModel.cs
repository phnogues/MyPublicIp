using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.LocalNetwork;

namespace MyPublicIp.Core.ViewModels;

/// <summary>"Network" tab: the adapter used to reach the internet and its settings, each one copyable.</summary>
public sealed partial class NetworkViewModel : ObservableObject
{
    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(1.5);

    private readonly ILocalNetworkInfo _network;
    private readonly IClipboard _clipboard;
    private readonly ILocalizer _localizer;
    private readonly TimeProvider _time;
    private NetworkDetails? _details;

    public NetworkViewModel(ILocalNetworkInfo network, IClipboard clipboard, ILocalizer localizer, TimeProvider time)
    {
        _network = network;
        _clipboard = clipboard;
        _localizer = localizer;
        _time = time;
        Refresh();
    }

    [ObservableProperty]
    public partial bool HasNetwork { get; private set; }

    /// <summary>"Wi-Fi · Home-5G", "Ethernet"...</summary>
    [ObservableProperty]
    public partial string ConnectionText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial int SignalBars { get; private set; }

    [ObservableProperty]
    public partial bool HasSignal { get; private set; }

    [ObservableProperty]
    public partial string LocalIPv4 { get; private set; } = "—";

    [ObservableProperty]
    public partial string Gateway { get; private set; } = "—";

    [ObservableProperty]
    public partial string DnsServers { get; private set; } = "—";

    [ObservableProperty]
    public partial string MacAddress { get; private set; } = "—";

    /// <summary>"Intel(R) Wi-Fi 6 AX201 · 866 Mbit/s"</summary>
    [ObservableProperty]
    public partial string AdapterText { get; private set; } = string.Empty;

    /// <summary>Key of the value just copied ("local", "gateway", "dns", "mac"), for the check mark.</summary>
    [ObservableProperty]
    public partial string? CopiedItem { get; private set; }

    public string? LocalIPv4Value => _details?.IPv4;

    public void Refresh()
    {
        _details = _network.GetPrimaryNetwork();
        HasNetwork = _details is not null;
        if (_details is null)
        {
            ConnectionText = _localizer.Get("NoNetwork");
            LocalIPv4 = Gateway = DnsServers = MacAddress = "—";
            AdapterText = string.Empty;
            HasSignal = false;
            return;
        }

        string kind = _details.Kind switch
        {
            ConnectionKind.WiFi => "Wi-Fi",
            ConnectionKind.Ethernet => "Ethernet",
            ConnectionKind.Cellular => _localizer.Get("Cellular"),
            _ => _details.AdapterName,
        };
        ConnectionText = string.IsNullOrWhiteSpace(_details.NetworkName) || _details.NetworkName == _details.AdapterName
            ? kind
            : $"{kind} · {_details.NetworkName}";
        HasSignal = _details.SignalBars is not null;
        SignalBars = _details.SignalBars ?? 0;
        LocalIPv4 = _details.IPv4 ?? "—";
        Gateway = _details.Gateway ?? "—";
        DnsServers = _details.DnsServers.Count > 0 ? string.Join("\n", _details.DnsServers) : "—";
        MacAddress = _details.MacAddress ?? "—";
        AdapterText = _details.LinkSpeedBitsPerSecond is { } speed
            ? $"{_details.AdapterDescription} · {FormatSpeed(speed)}"
            : _details.AdapterDescription;
    }

    internal static string FormatSpeed(long bitsPerSecond) => bitsPerSecond >= 1_000_000_000
        ? string.Create(CultureInfo.CurrentCulture, $"{bitsPerSecond / 1_000_000_000d:0.#} Gbit/s")
        : string.Create(CultureInfo.CurrentCulture, $"{bitsPerSecond / 1_000_000d:0} Mbit/s");

    [RelayCommand]
    private async Task CopyAsync(string? item)
    {
        string? value = item switch
        {
            "local" => _details?.IPv4,
            "gateway" => _details?.Gateway,
            "dns" => _details is { DnsServers.Count: > 0 } ? string.Join(", ", _details.DnsServers) : null,
            "mac" => _details?.MacAddress,
            _ => null,
        };

        if (value is null || !_clipboard.TrySetText(value))
        {
            return;
        }

        CopiedItem = item;
        await Task.Delay(CopiedFeedbackDuration, _time);
        if (CopiedItem == item)
        {
            CopiedItem = null;
        }
    }
}
