using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Keynest.Core.Vault;

namespace Keynest.Windows.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly VaultService _vault;

    [ObservableProperty] private bool _isLocked = true;
    [ObservableProperty] private bool _isFirstLaunch;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAppLocked;
    [ObservableProperty] private bool _isAppLockEnabled;

    public ObservableCollection<EntryViewModel> Entries { get; } = new();

    public MainViewModel(VaultService vault) => _vault = vault;

    public async Task InitialiseAsync()
    {
        IsBusy = true;
        IsFirstLaunch = await _vault.IsFirstLaunchAsync();
        if (!IsFirstLaunch)
        {
            var r = await _vault.LoadVaultAsync();
            if (r.Ok) PopulateEntries(r.Value!);
        }
        IsLocked = _vault.IsLocked;
        IsAppLocked = _vault.IsAppLocked;
        IsAppLockEnabled = _vault.IsAppLockEnabled;
        IsBusy = false;
    }

    public async Task CreateVaultAsync(string masterPassword)
    {
        IsBusy = true;
        var r = await _vault.CreateVaultAsync(masterPassword);
        if (r.Ok) { PopulateEntries(r.Value!); IsFirstLaunch = false; IsLocked = false; }
        IsBusy = false;
    }

    // Returns null on success, error message on failure.
    public async Task<string?> UnlockAsync(string masterPassword)
    {
        var r = await _vault.UnlockAsync(masterPassword);
        IsLocked = _vault.IsLocked;
        return r.Ok ? null : r.Error;
    }

    public async Task LockAsync()
    {
        await _vault.LockAsync();
        IsLocked = _vault.IsLocked;
    }

    public void UnlockEdit()
    {
        _vault.UnlockEdit();
        IsLocked = false;
    }

    public async Task<string?> CreateEntryAsync(string title, string username, string password, string shortcut)
    {
        var r = await _vault.CreateEntryAsync(title, username, password, shortcut);
        if (r.Ok) PopulateEntries(r.Value!);
        return r.Ok ? null : r.Error;
    }

    public async Task<string?> UpdateEntryAsync(string id, string title, string username, string password, string shortcut)
    {
        var r = await _vault.UpdateEntryAsync(id, title, username, password, shortcut);
        if (r.Ok) PopulateEntries(r.Value!);
        return r.Ok ? null : r.Error;
    }

    public async Task DeleteEntryAsync(string id)
    {
        var r = await _vault.DeleteEntryAsync(id);
        if (r.Ok) PopulateEntries(r.Value!);
    }

    public async Task SearchAsync(string query)
    {
        var r = await _vault.SearchAsync(query);
        if (r.Ok) PopulateEntries(r.Value!);
    }

    public string GeneratePassword() => _vault.GeneratePassword().Value ?? "";

    public async Task<string?> ChangeMasterPasswordAsync(string current, string newPw)
    {
        var r = await _vault.ChangeMasterPasswordAsync(current, newPw);
        return r.Ok ? null : r.Error;
    }

    public async Task<string?> ExportVaultAsync()
    {
        var r = await _vault.ExportVaultAsync();
        return r.Ok ? r.Value : null;
    }

    public async Task<(int Added, string? Error)> ImportVaultAsync(string json)
    {
        var r = await _vault.ImportVaultAsync(json);
        if (r.Ok) { PopulateEntries(r.Value!.Entries); return (r.Value.Added, null); }
        return (0, r.Error);
    }

    public async Task LockAppAsync()
    {
        await _vault.LockAppAsync();
        IsAppLocked = true;
        IsAppLockEnabled = true;
        IsLocked = true;
    }

    public async Task<string?> UnlockAppAsync(string masterPassword)
    {
        var r = await _vault.UnlockAppAsync(masterPassword);
        IsAppLocked = _vault.IsAppLocked;
        return r.Ok ? null : r.Error;
    }

    public async Task DisableAppLockAsync()
    {
        await _vault.DisableAppLockAsync();
        IsAppLocked = false;
        IsAppLockEnabled = false;
    }

    public async Task<bool> ResetVaultAsync()
    {
        var r = await _vault.ResetVaultAsync();
        if (r.Ok) { Entries.Clear(); IsFirstLaunch = true; IsLocked = true; }
        return r.Ok;
    }

    private void PopulateEntries(IReadOnlyList<VaultEntry> entries)
    {
        Entries.Clear();
        foreach (var e in entries) Entries.Add(EntryViewModel.FromEntry(e));
    }
}
