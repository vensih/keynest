using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace Keynest.Windows;

internal static class Program
{
    internal static readonly string LogPath = Path.Combine(Path.GetTempPath(), "keynest_error.log");

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Bootstrap.Initialize(0x00020000); // WAS 2.0
            Log("Bootstrap.Initialize OK");
        }
        catch (Exception ex)
        {
            Log($"Bootstrap.Initialize FAILED: {ex}");
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            new App();
        });

        Bootstrap.Shutdown();
        return 0;
    }

    internal static void Log(string msg) =>
        File.AppendAllText(LogPath, $"[{DateTime.Now:O}] {msg}\n");
}
