using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using MyPublicIp.Platform;

namespace MyPublicIp;

public static class Program
{
    public const string InstanceKey = "main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Only one tray icon: a second launch is forwarded to the running instance
        if (RedirectToRunningInstance())
        {
            return 0;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    private static bool RedirectToRunningInstance()
    {
        AppInstance mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (mainInstance.IsCurrent)
        {
            return false;
        }

        AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();

        // This process was just launched by the user: let the running instance bring its flyout to the front
        NativeMethods.AllowSetForegroundWindow(mainInstance.ProcessId);
        Task.Run(() => mainInstance.RedirectActivationToAsync(activation).AsTask()).Wait();
        return true;
    }
}
