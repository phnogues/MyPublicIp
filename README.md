<p align="center">
  <img src="docs/images/icon.png" alt="My Public IP Address icon" width="96" />
</p>

<h1 align="center">My Public IP Address</h1>

<p align="center">
  <b>Your public IP next to the Windows clock: where it is, whether your VPN is on, and how good your connection is.</b>
</p>

<p align="center">
  <a href="https://github.com/phnogues/MyPublicIp/actions/workflows/ci.yml"><img src="https://github.com/phnogues/MyPublicIp/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/WinUI-3-0078D4?logo=windows&logoColor=white" alt="WinUI 3" />
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4" alt="Windows 10 and 11" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="MIT license" /></a>
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9NX8XP3BX8KM">
    <img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Get it from Microsoft" width="200" />
  </a>
</p>

<p align="center">
  <img src="docs/media/trailer.webp" alt="30-second trailer: the flyout, the notification when the IP changes, the tooltip and the menu" width="720" />
  <br />
  <sub>Full-quality video: <a href="https://github.com/phnogues/MyPublicIp/raw/main/docs/media/trailer.mp4">trailer.mp4</a></sub>
</p>

---

Setting up a VPN, opening a port, whitelisting your office in a firewall, answering a support
technician: sooner or later you need your **public** IP address, and to know whether your VPN is
really on. Instead of opening a browser on a page full of ads, *My Public IP Address* keeps it all in
the notification area, always up to date, and tells you when it changes.

## Features

- **Overview**: public IPv4 and IPv6, the country, city and internet provider behind them, VPN status,
  and the live latency with a small chart and a quality rating.
- **Network**: Wi-Fi or Ethernet, network name and signal, local IP, gateway, DNS servers, MAC address,
  adapter and link speed. Every value is copied in one click.
- **History**: the recent public addresses with their date, country and VPN.
- **VPN at a glance**: a green dot on the tray icon while a VPN is connected.
- **Notifications** when the public IP changes or the VPN connects or drops, with a button to copy the new address.
- **Keyboard shortcut**: Win+Shift+I copies the public IP from any app.
- **Tray icon**: tooltip with the IP, location and VPN; right-click menu with copy, refresh and settings
  (also opens with the keyboard).
- **Always current**: refreshed when the network changes (Wi-Fi, VPN, Ethernet...), every 15 minutes,
  and retried quickly after a failure. An error is never shown or copied as if it were an address.
- **Starts with Windows**, quietly. Can be turned off from the app or from Windows Settings.
- **Native look**: acrylic flyout with tabs, light and dark themes, follows the taskbar position and display scaling.
- **Private**: no account, no ads, no tracking; the history stays on your PC (see [PRIVACY.md](PRIVACY.md)).
- English and French.

## Screenshots

| Overview | Network |
| :---: | :---: |
| <img src="docs/images/overview.png" alt="Overview tab: public IPv4 and IPv6, location and provider, VPN status and live latency" width="360" /> | <img src="docs/images/network.png" alt="Network tab: connection, local IP, gateway, DNS servers and MAC address" width="360" /> |
| **History** | **Notification when the IP changes** |
| <img src="docs/images/history.png" alt="History tab: recent public addresses with country and VPN" width="360" /> | <img src="docs/images/notification.png" alt="Windows notification: public IP changed and VPN connected, with a button to copy the new address" width="360" /> |
| **Tray icon with VPN dot and menu** | **Light theme** |
| <img src="docs/images/menu.png" alt="Right-click menu of the tray icon, with the green VPN dot on the icon" width="360" /> | <img src="docs/images/light.png" alt="Overview tab in the light theme" width="360" /> |

<sub>Screenshots use addresses reserved for documentation (RFC 5737 and RFC 3849) and a fictitious provider.</sub>

## Architecture

The solution separates the behavior from the Windows shell around it:

```mermaid
flowchart LR
    subgraph App["MyPublicIp · WinUI 3 app"]
        Tray["TrayIconController<br/><i>icon, VPN dot, tooltip, menu</i>"]
        Flyout["FlyoutWindow<br/><i>tabs, XAML + x:Bind</i>"]
        Platform["Platform services<br/><i>startup task, clipboard, settings, notifications,<br/>global hotkey, Wi-Fi profile, JSON history</i>"]
    end

    subgraph Core["MyPublicIp.Core · net10.0, no Windows dependency"]
        Main["MainViewModel"]
        Children["Connection, Network and<br/>Settings view models"]
        Monitor["PublicIpMonitor<br/><i>refresh policy</i>"]
        Resolver["HttpPublicIpResolver"]
        Geo["HttpGeoLocator<br/><i>cache per address</i>"]
        Latency["LatencyMonitor<br/><i>adaptive rate</i>"]
        Adapters["AdapterSelection<br/><i>primary adapter, VPN</i>"]
        History["IpHistory<br/><i>change detection</i>"]
    end

    Services[("ipify · icanhazip · AWS<br/>ipinfo · ipapi · 1.1.1.1")]

    Tray --> Main
    Flyout --> Main
    Main --> Children
    Main --> Monitor --> Resolver --> Services
    Main --> History
    Children --> Geo --> Services
    Children --> Latency --> Services
    Children --> Adapters
    Main -. "interfaces" .-> Platform
```

- **`MyPublicIp.Core`** holds all the logic: lookup, refresh policy, geolocation, latency, adapter
  and VPN detection, change detection and history, view models and flyout placement. It only depends
  on .NET and [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), so it is fully unit tested.
- **`MyPublicIp`** is a thin WinUI 3 shell: the composition root ([`App.xaml.cs`](src/MyPublicIp/App.xaml.cs)),
  the views, and the Windows implementations of the Core interfaces.

### Design notes

| Topic | Choice |
| --- | --- |
| UI pattern | MVVM with source-generated properties and commands: a main view model for the public address and history, child view models for the Connection, Network and Settings tabs, shared by the flyout and the tray menu. |
| Refresh policy | Periodic refresh, network changes debounced (adapters often change in bursts), increasing backoff after failures (3 s to 1 min), concurrent requests coalesced. |
| Change detection | A change is a new IPv4 or a new IPv6 /64 network: IPv6 privacy extensions rotate the rest of the address every day, which must not raise a notification. The first result of a session is recorded silently. |
| Testability | Time is injected with `TimeProvider`: refresh and backoff tests advance a fake clock instead of sleeping. |
| Robustness | Answers are parsed strictly (`IPAddress.TryParse` alone accepts `"1"`); one failing service falls back to the next; the clipboard is retried when another app holds it; the history is written atomically. |
| VPN detection | Windows has no API for third-party VPNs: the adapters are classified (tunnel and PPP types, names of common VPN clients, system tunnels such as Teredo excluded) by pure functions covered by tests. |
| Latency | ICMP ping to 1.1.1.1, falling back to a TCP connect time when ICMP is blocked; every 2 s while the flyout is open, once a minute otherwise. |
| Geolocation | Looked up only when the public address changes, cached per address, with a second service as fallback. |
| Global hotkey | `RegisterHotKey` on a message-only window whose procedure is an `UnmanagedCallersOnly` function pointer: no delegate to keep alive, compatible with trimming. |
| Notifications | Windows App SDK `AppNotificationManager`, with the COM activator declared in the manifest: a click reaches the running instance, or starts the app if it was closed. |
| Start with Windows | MSIX `windows.startupTask` instead of a Startup-folder shortcut, which a packaged app cannot create. A choice made in Windows Settings is respected. |
| Tray icon identity | Windows ties a tray icon GUID to the executable path, and every MSIX update changes that path: the GUID is derived from the path so the icon always comes back after an update. |
| Single instance | A second launch is redirected to the running instance with `AppInstance`, which then shows its flyout. |
| Packaging | Single-project MSIX with a self-contained, fully trimmed .NET 10 runtime: about 8 MB per architecture (x86 / x64 / ARM64) instead of 60 MB. JSON uses source generation, so nothing relies on reflection. |

## Project structure

```
├── src
│   ├── MyPublicIp.Core              Platform-independent logic (net10.0)
│   │   ├── Abstractions             Interfaces implemented by the app (startup, clipboard, settings...)
│   │   ├── PublicIp                 Resolver, monitor, refresh policy, snapshot
│   │   ├── History                  Address history and change detection
│   │   ├── Geo                      Location and provider of the public address
│   │   ├── Latency                  Latency probe, monitor and quality rating
│   │   ├── LocalNetwork             Adapter details and VPN detection
│   │   ├── Layout                   Flyout placement next to the clock
│   │   └── ViewModels               Main, Connection, Network and Settings view models
│   └── MyPublicIp                   WinUI 3 app (MSIX)
│       ├── Views                    Flyout window
│       ├── Tray                     Notification area icon and menu
│       ├── Platform                 Windows implementations, notifications, hotkey, Win32 interop
│       ├── Diagnostics              Debug-only demo mode for screenshots
│       ├── Strings                  en-US and fr-FR resources
│       └── Package.appxmanifest     Store identity, capabilities, startup task, notification activation
├── tests
│   └── MyPublicIp.Core.Tests        xUnit v3 tests of the Core
├── tools/generate-assets.py         Generates every icon of the package
├── Directory.Build.props            Shared build settings (warnings as errors, analyzers)
└── Directory.Packages.props         Central NuGet versions
```

## Build and run

Requirements: Windows 10 (1809) or 11, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
and, to run the app, [Developer Mode](https://learn.microsoft.com/windows/apps/get-started/enable-your-device-for-development).

**Visual Studio 2026**: open `MyPublicIp.slnx` (workload *WinUI application development*), select `x64` and press F5.

**Command line**:

```powershell
# Build and run the tests
dotnet build MyPublicIp.slnx -p:Platform=x64
dotnet test --project tests/MyPublicIp.Core.Tests/MyPublicIp.Core.Tests.csproj

# Register the app for the current user, then start "My Public IP Address" from the Start menu
Add-AppxPackage -Register src/MyPublicIp/bin/x64/Debug/net10.0-windows10.0.19041.0/win-x64/AppxManifest.xml
```

Debug builds accept `--demo` (documentation addresses, a sample history, and a new address at each
refresh to show the notification), `--theme=light|dark` and `--lang=en-US|fr-FR`. They were used for the screenshots:

```powershell
$package = Get-AppxPackage 16566phsoftwares.MyPublicIPAddress
Invoke-CommandInDesktopPackage -PackageFamilyName $package.PackageFamilyName -AppId App `
    -Command (Join-Path $package.InstallLocation MyPublicIp.exe) -Args "--demo --theme=dark --lang=en-US"
```

## Publish to the Microsoft Store

In Visual Studio: right-click the `MyPublicIp` project > **Package and Publish** > **Create App Packages**.
From the command line, with the Visual Studio MSBuild:

```powershell
MSBuild src/MyPublicIp/MyPublicIp.csproj -restore -p:Configuration=Release -p:Platform=x64 `
    -p:GenerateAppxPackageOnBuild=true -p:UapAppxPackageBuildMode=StoreUpload `
    "-p:AppxBundlePlatforms=x86|x64|arm64" -p:AppxBundle=Always
```

The `.msixupload` file is written to `artifacts/AppPackages/`. Increase `Version` in
[`Package.appxmanifest`](src/MyPublicIp/Package.appxmanifest) before each submission.

## Privacy

The app only sends a plain HTTPS request to public "what is my IP" services. It collects nothing,
has no analytics and no account, and the address history never leaves your PC. Details in [PRIVACY.md](PRIVACY.md).

## License

[MIT](LICENSE) © Pierre-Henri Nogues
