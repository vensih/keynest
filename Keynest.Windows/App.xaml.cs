using Keynest.Core.Vault;
using Keynest.Windows.Services;
using Keynest.Windows.ViewModels;
using Microsoft.UI.Xaml;

namespace Keynest.Windows;

public partial class App : Application
{
    public static MainWindow? CurrentWindow { get; private set; }
    internal MainWindow? _window;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            var ex = e.Exception;
            var msg = $"[{DateTime.Now:O}] Unhandled: {ex}";
            if (ex.InnerException is not null)
                msg += $"\n  Inner: {ex.InnerException}";
            if (ex is System.Runtime.InteropServices.COMException com)
                msg += $"\n  HResult: 0x{com.HResult:X8}";
            Program.Log(msg);
        };

        var vault = new VaultService();
        await vault.InitAsync(new WindowsVaultStorage(), new WindowsCredentialVerifier());

        _window = new MainWindow(new MainViewModel(vault));
        CurrentWindow = _window;
        _window.Activate();
    }
}
