namespace Keynest.Core.Abstractions;

/// <summary>
/// Resolves the directory where vault files are stored.
/// Windows:  %APPDATA%\Keynest\
/// macOS:    ~/Library/Application Support/Keynest/
/// Linux:    $XDG_CONFIG_HOME/keynest/  (fallback: ~/.config/keynest/)
/// </summary>
public interface IVaultStorage
{
    string GetVaultDirectory();
}
