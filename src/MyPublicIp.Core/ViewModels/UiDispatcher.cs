namespace MyPublicIp.Core.ViewModels;

/// <summary>
/// Brings events raised on background threads back to the thread that created the view model
/// (the UI thread in the app, the test thread in unit tests, where there is no context).
/// </summary>
internal sealed class UiDispatcher
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    public void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }
}
