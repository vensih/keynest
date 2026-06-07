using Keynest.Core.Abstractions;

namespace Keynest.Windows.Services;

public sealed class WindowsVaultStorage : IVaultStorage
{
    public string GetVaultDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Keynest");
}
